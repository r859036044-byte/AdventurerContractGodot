// 原 RM 股票／槓桿腳本的窄範圍轉接；結算時機仍由原事件負責。
using System;
using System.Globalization;
using System.Text.RegularExpressions;
namespace RMZ {
public static class RMZFinanceScripts {
 static readonly Regex Market = new(@"const\s+vId\s*=\s*(603|604)\s*;");
 static readonly Regex Chance = new(@"roll\s*<\s*([0-9.]+)");
 static readonly Regex Rate = new(@"const\s+rate\s*=\s*Math.random\(\)\s*\*\s*\(([0-9.]+)\s*-\s*([0-9.]+)\)\s*\+\s*([0-9.]+)");
 static double N(string s) => double.Parse(s, CultureInfo.InvariantCulture);
 static int Money(double n) => (int)Math.Clamp(Math.Floor(n + .5), 0, int.MaxValue);
 public static bool TryRun(string block) {
  var s = Regex.Replace(block, @"//[^\r\n]*", "");
  var m=Market.Match(s); var c=Chance.Match(s); var rates=Rate.Matches(s);
  if(m.Success && c.Success && rates.Count==2 && s.Contains("$gameVariables.setValue(vId, newVal)")) {
   int id=int.Parse(m.Groups[1].Value), flag=id==603?609:610;
   double roll=Random.Shared.NextDouble();
   bool win=(s.Contains("const guaranteed") && RMZGameState.Variable(702)==1) || roll<N(c.Groups[1].Value);
   var r=rates[win?0:1]; double rate=Random.Shared.NextDouble()*(N(r.Groups[1].Value)-N(r.Groups[2].Value))+N(r.Groups[3].Value);
   RMZGameState.SetVariable(id,Money(Math.Max(0,RMZGameState.Variable(id))*(win?1+rate:1-rate)));
   // 明確歸零，避免上一天成功旗標殘留，令下跌成就與提示判斷錯誤。
   RMZGameState.SetVariable(flag,win?1:0);
   RMZEventRunner.Instance?.RequestRefresh(); return true;
  }
  var scale=Regex.Match(s.Trim(), @"^(?:let|var|const)\s+oldValue\s*=\s*\$gameVariables.value\((\d+)\);\s*\$gameVariables.setValue\(\1,\s*Math.round\(oldValue\s*\*\s*([0-9.]+)\)\);?$");
  if(scale.Success) { int id=int.Parse(scale.Groups[1].Value); RMZGameState.SetVariable(id,Money(RMZGameState.Variable(id)*N(scale.Groups[2].Value))); RMZEventRunner.Instance?.RequestRefresh(); return true; }
  return false;
 }
}
}
