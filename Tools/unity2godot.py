#!/usr/bin/env python3
"""Unity C# -> Godot C# 的機械式轉換前處理。
只做「絕對安全」的字串替換，其餘（MonoBehaviour、Vector 大小寫、生命週期）
一律留下 TODO 標記讓人工處理，不做猜測。

用法: python3 unity2godot.py <來源.cs> <輸出.cs>
"""
import re, sys

SAFE = [
    (r'using UnityEngine;', 'using Godot;'),
    (r'using UnityEngine\.UI;', '// [移植] UnityEngine.UI -> Godot Control'),
    (r'\bDebug\.LogError\(', 'GD.PushError('),
    (r'\bDebug\.LogWarning\(', 'GD.PushWarning('),
    (r'\bDebug\.Log\(', 'GD.Print('),
    (r'\bMathf\.Deg2Rad\b', 'Mathf.Pi / 180f'),
    (r'\bMathf\.Rad2Deg\b', '180f / Mathf.Pi'),
    (r'\bMathf\.Infinity\b', 'float.PositiveInfinity'),
    (r'\bMathf\.Epsilon\b', 'float.Epsilon'),
    (r'\bMathf\.PI\b', 'Mathf.Pi'),
]

# 這些 Unity 屬性在 Godot 沒有對應，直接刪掉整行
DROP_ATTR = re.compile(r'^\s*\[(Tooltip|Header|Range|SerializeField|HideInInspector|Space|TextArea|RequireComponent)\b.*\]\s*$')
# 同一行既有屬性又有程式碼時（例如 [Range(0f,1f)] public float X;），只拿掉屬性那一段
INLINE_ATTR = re.compile(r'\[(Tooltip|Header|Range|SerializeField|HideInInspector|Space|TextArea|RequireComponent)\b[^\]]*\]\s*')
# 這些出現時代表需要人工重寫，標記出來
FLAG = [
    ('MonoBehaviour', 'MonoBehaviour -> Node/Node2D，並把 Awake/Start/Update 改成 _Ready/_Process'),
    ('GameObject', 'GameObject -> Node'),
    ('RectTransform', 'RectTransform -> Control'),
    ('Coroutine', 'Coroutine -> async/await 或 _Process 狀態機'),
    ('StartCoroutine', 'StartCoroutine -> async/await'),
    ('Time.deltaTime', 'Time.deltaTime -> _Process(double delta) 的 delta'),
    ('Resources.Load', 'Resources.Load -> GD.Load / RMZImageCache'),
]

def convert(text):
    out = []
    for line in text.splitlines():
        if DROP_ATTR.match(line):
            continue
        line = INLINE_ATTR.sub('', line)
        for pat, rep in SAFE:
            line = re.sub(pat, rep, line)
        out.append(line)
    text = '\n'.join(out) + '\n'

    notes = sorted({msg for key, msg in FLAG if key in text})
    if notes:
        head = '// [移植待辦]\n' + ''.join(f'//   - {n}\n' for n in notes)
        text = head + text
    return text

if __name__ == '__main__':
    src, dst = sys.argv[1], sys.argv[2]
    open(dst, 'w', encoding='utf-8').write(convert(open(src, encoding='utf-8').read()))
    print(f'轉換完成: {dst}')
