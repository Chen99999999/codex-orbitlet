using System;
using System.Web.Script.Serialization;
using Orbitlet;
public class QuotaTests {
    static int count;
    static void Equal(double? actual,double? expected,string label) {
        if(actual!=expected)throw new Exception(label+": "+actual+" != "+expected);
        count++;
    }
    static QuotaSnapshot Parse(string json) {
        return CodexQuotaClient.Parse(new JavaScriptSerializer().DeserializeObject(json),new DateTime(2026,10,26,12,0,0,DateTimeKind.Utc));
    }
    public static void Main() {
        var multi=Parse("{\"rateLimits\":{\"limitId\":\"codex_other\",\"primary\":{\"windowDurationMins\":300,\"usedPercent\":99}},\"rateLimitsByLimitId\":{\"codex\":{\"limitId\":\"codex\",\"primary\":{\"windowDurationMins\":300,\"usedPercent\":67},\"secondary\":{\"windowDurationMins\":10080,\"usedPercent\":26}},\"codex_other\":{}}}");
        Equal(multi.FiveHourRemainingPercent,33,"Prefer Codex bucket");
        Equal(multi.WeeklyRemainingPercent,74,"Remaining vs used");
        var reversed=Parse("{\"rateLimits\":{\"primary\":{\"windowDurationMins\":10080,\"usedPercent\":75},\"secondary\":{\"windowDurationMins\":300,\"usedPercent\":90}}}");
        Equal(reversed.FiveHourRemainingPercent,10,"Match 300 by duration");
        Equal(reversed.WeeklyRemainingPercent,25,"Match 10080 by duration");
        var unknown=Parse("{\"rateLimits\":{\"primary\":{\"windowDurationMins\":300,\"usedPercent\":null},\"secondary\":{\"windowDurationMins\":15,\"usedPercent\":0}}}");
        Equal(unknown.FiveHourRemainingPercent,null,"Null is unknown");
        Equal(unknown.WeeklyRemainingPercent,null,"Different duration is unknown");
        var limits=Parse("{\"rateLimits\":{\"primary\":{\"windowDurationMins\":300,\"usedPercent\":120},\"secondary\":{\"windowDurationMins\":10080,\"usedPercent\":-10}}}");
        Equal(limits.FiveHourRemainingPercent,0,"Clamp lower bound");
        Equal(limits.WeeklyRemainingPercent,100,"Clamp upper bound");
        var zero=Parse("{\"rateLimits\":{\"primary\":{\"windowDurationMins\":300,\"usedPercent\":0},\"secondary\":{\"windowDurationMins\":10080,\"usedPercent\":100}}}");
        Equal(zero.FiveHourRemainingPercent,100,"Zero used is full remaining");
        Equal(zero.WeeklyRemainingPercent,0,"Full used is zero remaining");
        bool wrong=false;
        try {
            Parse("{\"rateLimitsByLimitId\":{\"codex_other\":{}},\"rateLimits\":{\"primary\":{\"windowDurationMins\":300,\"usedPercent\":0}}}");
        }
        catch(QuotaReadException) {
            wrong=true;
        }
        if(!wrong)throw new Exception("Wrong bucket must not be presented as Codex");
        count++;
        string fresh=multi.ToJson("ok",null),stale=multi.ToJson("error","offline");
        var serializer=new JavaScriptSerializer();
        var a=Json.Map(serializer.DeserializeObject(fresh));
        var b=Json.Map(serializer.DeserializeObject(stale));
        if(Convert.ToString(a["updatedAtUtc"])!=Convert.ToString(b["updatedAtUtc"]))throw new Exception("Failure changed successful timestamp");
        count++;
        if(fresh.Contains("accountId")||fresh.Contains("access_token")||fresh.Contains("credits"))throw new Exception("Unexpected account data in public state");
        count++;
        Console.WriteLine("PASS: "+count+" quota mapping, missing-data, stale timestamp and data-minimization assertions.");
    }
}
