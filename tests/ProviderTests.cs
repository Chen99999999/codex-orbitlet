using System;
using System.Web.Script.Serialization;
using Orbitlet;

public static class ProviderTests {
    static readonly JavaScriptSerializer Serializer=new JavaScriptSerializer();
    static readonly DateTime Now=new DateTime(2026,11,19,0,0,0,DateTimeKind.Utc);
    static int assertions;

    static void Check(bool condition,string message) {
        if(!condition)throw new Exception(message);
        assertions++;
    }

    static QuotaSnapshot Read(string provider,string json) {
        return QuotaSources.Parse(QuotaSources.Find(provider),Serializer.DeserializeObject(json),Now);
    }

    static void Reject(string provider,string json) {
        try {
            Read(provider,json);
        }
        catch(QuotaReadException) {
            assertions++;
            return;
        }
        throw new Exception(provider+" returned a successful snapshot for unavailable data.");
    }

    static void Run() {
        var qoder=Read("qoder","{\"expiresAt\":1796000000123,\"userQuota\":{\"total\":100,\"used\":32},\"addOnQuota\":{\"total\":10,\"remaining\":0}}");
        Check(qoder.FirstPercent==68&&qoder.SecondPercent==0&&qoder.FirstEnd==1796000000,"Qoder converts actual buckets and epoch milliseconds.");
        var org=Read("qoder-cn","{\"orgResourcePackage\":{\"available\":true,\"cap\":200,\"remaining\":50}}");
        Check(org.FirstPercent==null&&org.SecondPercent==25&&org.InnerLabel=="组织","Organization bucket is labeled and independent.");
        Reject("qoder","{\"session\":{\"total_credits\":42}}");
        Reject("qoder-cn","{\"userQuota\":{\"total\":0,\"remaining\":0},\"orgResourcePackage\":{\"available\":false,\"cap\":200,\"remaining\":50}}");

        var claude=Read("claude","{\"rate_limits\":{\"five_hour\":{\"used_percentage\":0,\"resets_at\":1796000000},\"seven_day\":{\"used_percentage\":75,\"resets_at\":1700000000}}}");
        Check(claude.FirstPercent==100&&claude.SecondPercent==null&&claude.SecondEnd==null,"Claude preserves a valid zero and drops an expired window.");
        Reject("claude","{\"rate_limits\":{\"spend_limit\":{\"used_percentage\":10}},\"context_window\":{\"used_percentage\":40}}");

        var glm=Read("zai","{\"data\":{\"limits\":[{\"type\":\"TOKENS_LIMIT\",\"percentage\":18},{\"type\":\"TOKENS_LIMIT\",\"percentage\":90,\"nextResetTime\":1796000000000},{\"type\":\"TIME_LIMIT\",\"percentage\":10},{\"type\":\"UNKNOWN\",\"percentage\":100}]}}");
        Check(glm.FirstPercent==10&&glm.SecondPercent==90&&glm.FirstEnd==1796000000,"Multiple GLM windows use the tightest recognized bucket.");
        Reject("bigmodel","{\"data\":{\"limits\":[{\"type\":\"UNKNOWN\",\"percentage\":10}]}}");

        var minimax=Read("minimax","{\"model_remains\":[{\"model_name\":\"MiniMax-M*\",\"start_time\":1795982000000,\"end_time\":1796000000000,\"current_interval_remaining_percent\":69,\"current_interval_total_count\":0,\"current_weekly_total_count\":1000,\"current_weekly_usage_count\":190},{\"model_name\":\"image-01\",\"current_weekly_remaining_percent\":0}]}");
        Check(minimax.FirstPercent==69&&minimax.SecondPercent==19&&minimax.FirstEnd==1796000000,"MiniMax uses explicit percentages, the vendor's legacy remaining counts and text models only.");
        Reject("minimax-cn","{\"model_remains\":[{\"model_name\":\"MiniMax-M*\",\"start_time\":1795900000000,\"end_time\":1796000000000,\"current_interval_remaining_percent\":42,\"current_weekly_total_count\":0,\"current_weekly_remaining_percent\":100,\"current_weekly_status\":3}]}");

        var unlimited=Read("openrouter","{\"data\":{\"limit\":null,\"limit_remaining\":null,\"usage\":0,\"label\":\"personal-key-label\"}}");
        Check(unlimited.FirstPercent==null&&unlimited.SecondPercent==null&&unlimited.InnerAmount=="$0","Unlimited key usage stays a currency amount.");
        Check(!unlimited.ToJson("ok",null).Contains("personal-key-label"),"Raw API key metadata must not reach the local preview.");
        var limited=Read("openrouter","{\"data\":{\"limit\":50,\"limit_remaining\":10,\"usage\":40,\"is_management_key\":false}}");
        Check(limited.FirstPercent==20&&limited.InnerAmount=="$40","OpenRouter balance ratio uses the actual cap.");

        var deepseek=Read("deepseek","{\"balance_infos\":[{\"currency\":\"USD\",\"total_balance\":\"12.50\",\"granted_balance\":\"0.00\"},{\"currency\":\"CNY\",\"total_balance\":\"110.00\",\"granted_balance\":\"20.00\"}]}");
        Check(deepseek.FirstPercent==null&&deepseek.OuterAmount=="$12.5"&&deepseek.InnerAmount=="$0"&&deepseek.Detail.Contains("CNY 110"),"DeepSeek preserves currency and zero balances.");
        var kimi=Read("moonshot-global","{\"code\":0,\"status\":true,\"data\":{\"available_balance\":0,\"cash_balance\":-3.5}}");
        Check(kimi.OuterAmount=="$0"&&kimi.InnerAmount=="$-3.5"&&kimi.SecondPercent==null,"Kimi API preserves signed amounts and regional currency.");
        Reject("moonshot","{\"code\":1,\"data\":{\"cash_balance\":0}}");

        var preview=Json.Map(Serializer.DeserializeObject(qoder.ToJson("ok",null)));
        Check(Json.Get(preview,"fiveHourRemainingPercent")==null&&Json.Number(Json.Get(preview,"outerPercent"))==68,"Non-Codex values cannot be mistaken for Codex windows.");
        Check(DesktopTargets.Find("qoder").MatchesName("QoderCN")&&!DesktopTargets.Find("qoder").MatchesName("qodercli"),"Qoder CLI is excluded from desktop detection.");
        Check(DesktopTargets.Find("vscode").MatchesName("Code - Insiders")&&!DesktopTargets.Find("cursor").MatchesName("Code"),"Editor identity uses the selected executable names.");
        Check(DesktopTargets.Find("unsupported")==null&&QuotaSources.Find("unsupported")==null,"Unknown provider and host IDs are rejected.");
    }

    public static int Main() {
        try {
            Run();
            Console.WriteLine("PASS: "+assertions+" provider, authentication-data, expiry and window identity checks.");
            return 0;
        }
        catch(Exception error) {
            Console.Error.WriteLine(error.Message);
            return 1;
        }
    }
}
