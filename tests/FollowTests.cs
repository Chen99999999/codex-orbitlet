using System;
using Orbitlet;
class FollowTests {
    static int count;
    static void Assert(bool condition,string label) {
        if(!condition)throw new Exception(label);
        count++;
    }
    public static void Main() {
        string family="OpenAI.Codex_2p2nqsd0c76g0";
        Assert(CodexDesktop.IsDesktopIdentity("ChatGPT",family,null),"Official desktop recognized");
        Assert(!CodexDesktop.IsDesktopIdentity("codex",family,"C:\\tools\\codex.exe"),"CLI excluded even with inherited package identity");
        Assert(!CodexDesktop.IsDesktopIdentity("ChatGPT","OpenAI.ChatGPT_2p2nqsd0c76g0",null),"Different ChatGPT app excluded");
        Assert(CodexDesktop.IsDesktopIdentity("ChatGPT",null,"C:\\Program Files\\WindowsApps\\OpenAI.Codex_99.0_x64__publisher\\app\\ChatGPT.exe"),"Version update fallback recognized");
        Assert(!CodexDesktop.IsDesktopIdentity("ChatGPT",null,"C:\\Other\\ChatGPT.exe"),"Unrelated executable excluded");
        var now=new DateTime(2026,10,27,0,0,0,DateTimeKind.Utc);
        var absence=new DesktopAbsenceGuard();
        Assert(!absence.Observe(false,now),"Close grace begins");
        Assert(!absence.Observe(false,now.AddSeconds(5)),"Brief close ignored");
        Assert(absence.Observe(false,now.AddSeconds(6)),"Confirmed close exits");
        Assert(!absence.Observe(true,now.AddSeconds(7)),"Visible/minimized desktop resets close grace");
        Assert(!absence.Observe(false,now.AddSeconds(8)),"New close starts fresh grace");
        Assert(!absence.Observe(null,now.AddSeconds(20)),"Inspection failure cannot imply exit");
        var gate=new DesktopLaunchGate();
        Assert(gate.Observe(true,now),"Starts on existing desktop at watcher launch");
        Assert(!gate.Observe(true,now.AddSeconds(2)),"Never duplicates or respawns after a manual widget exit");
        Assert(!gate.Observe(false,now.AddSeconds(4)),"Closing does not launch");
        Assert(!gate.Observe(true,now.AddSeconds(6)),"Fast desktop restart does not flicker");
        Assert(!gate.Observe(false,now.AddSeconds(10)),"Second close begins");
        Assert(!gate.Observe(false,now.AddSeconds(16)),"Confirmed closed transition arms next launch");
        Assert(gate.Observe(true,now.AddSeconds(18)),"Reopening desktop relaunches widget");
        Assert(!gate.Observe(null,now.AddSeconds(20)),"Unknown state cannot launch");
        Assert(!gate.Observe(true,now.AddSeconds(22)),"Recovery from unknown never duplicates");
        Console.WriteLine("PASS: "+count+" desktop identity, CLI exclusion, close grace, minimize, manual exit and reopen assertions.");
    }
}
