#if PICKLEVERSE_LEGACY_NGO
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Services.Core.Editor.Environments;
using Unity.Services.DeploymentApi.Editor;
using UnityEditor;
using UnityEngine;

namespace PickleVerse.EditorTools
{
    public static class MultiplayerCloudSetup
    {
        public static string Status { get; private set; }="Chưa triển khai";
        const string Folder="Assets/Settings/Multiplayer/Cloud";
        [MenuItem("PickleVerse/Multiplayer/Deploy Test Configuration")]
        public static async void Deploy() { await DeployAsync(); }
        public static async Task DeployAsync()
        { await DeploySelectedAsync(false); }
        [MenuItem("PickleVerse/Multiplayer/Deploy Ranked 1v1 Test Configuration")]
        public static async void DeployRanked() { await DeployRankedAsync(); }
        public static Task DeployRankedAsync() => DeploySelectedAsync(true);
        static async Task DeploySelectedAsync(bool rankedOnly)
        {
            try
            {
                Status="Đang chuẩn bị cấu hình test";
                WriteConfigs(); AssetDatabase.Refresh();
                var api=EnvironmentsApi.Instance; await api.RefreshAsync();
                if(!api.Environments.Any(e=>e.Name=="pickleverse-test")) await CreateTestEnvironmentAsync(api);
                await api.RefreshAsync(); api.SetActiveEnvironment("pickleverse-test");
                var provider=Deployments.Instance.DeploymentProviders.First(p=>p.Service=="Matchmaker");
                // Only these three owned config assets are deployed, never unrelated queues.
                var items=provider.DeploymentItems.Where(i=>i.Path.StartsWith(Folder+"/",StringComparison.Ordinal) && (!rankedOnly || i.Path.EndsWith("PickleVerse_1v1.mmq",StringComparison.Ordinal))).ToArray();
                if(items.Length!=(rankedOnly?1:3)) throw new InvalidOperationException("Cấu hình test chưa được import đầy đủ.");
                await provider.DeployCommand.ExecuteAsync(items);
                Status=string.Join("\n",items.Select(i=>i.Name+": "+i.Status.Message+" "+i.Status.MessageDetail));
                File.WriteAllText("Docs/MultiplayerCloudDeployment.txt",DateTime.UtcNow.ToString("O")+"\nEnvironment: pickleverse-test\n"+Status);
                Debug.Log(Status);
            }
            catch(Exception e) { Status="Triển khai test thất bại: "+e.GetBaseException().Message; Debug.LogWarning(Status); }
        }
        public static void WriteConfigs()
        {
            Directory.CreateDirectory(Folder);
            foreach(int size in new[]{1,2})
            {
                var json=new JObject
                {
                    ["$schema"]="https://ugs-config-schemas.unity3d.com/v1/matchmaker/matchmaker-queue.schema.json",
                    ["name"]="PickleVerse-"+size+"v"+size,["enabled"]=true,["maxPlayersPerTicket"]=size,
                    ["defaultPool"]=new JObject
                    {
                        ["name"]="LowPing",["enabled"]=true,["timeoutSeconds"]=size==1?(int)PickleVerse.Progression.RankedSearchGate.DeadlineSeconds:120,
                        ["matchHosting"]=new JObject { ["type"]="MatchId" },
                        ["matchLogic"]=new JObject
                        {
                            ["name"]="Rules",["backfillEnabled"]=false,
                            ["matchDefinition"]=new JObject
                            {
                                ["teams"]=new JArray(new JObject { ["name"]="Team",["teamCount"]=new JObject { ["min"]=2,["max"]=2 },["playerCount"]=new JObject { ["min"]=size,["max"]=size },["teamRules"]=new JArray() }),
                                ["matchRules"]=new JArray()
                            }
                        }
                    },["filteredPools"]=new JArray()
                };
                var rules=(JArray)json["defaultPool"]["matchLogic"]["matchDefinition"]["matchRules"];
                // Partition tickets by their own protocol instead of pinning the queue to one build.
                // Old/new clients can search simultaneously, but never share a network match.
                rules.Add(new JObject { ["name"]="SameProtocol",["type"]="Equality",["source"]="Players.CustomData.protocol",["enableRule"]=true });
                if(size==1)
                {
                    rules.Add(new JObject { ["name"]="EloWindow",["type"]="Difference",["source"]="Players.CustomData.elo",["reference"]=120,["enableRule"]=true });
                    rules.Add(new JObject { ["name"]="MutualPreference",["type"]="Intersection",["source"]="Players.CustomData.pairingKeys",["overlap"]=1,["enableRule"]=true });
                }
                File.WriteAllText(Folder+"/PickleVerse_"+size+"v"+size+".mmq",json.ToString(Formatting.Indented));
            }
            File.WriteAllText(Folder+"/PickleVerseTest.mme",new JObject { ["$schema"]="https://ugs-config-schemas.unity3d.com/v1/matchmaker/matchmaker-environment-config.schema.json",["enabled"]=true,["defaultQueueName"]="PickleVerse-1v1" }.ToString());
        }
        static async Task CreateTestEnvironmentAsync(IEnvironmentsApi api)
        {
            // The public editor environment API does not expose creation. Reuse its authenticated generated client.
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
            var service=api.GetType().GetField("m_EnvironmentService",flags).GetValue(api);
            var fetcher=service.GetType().GetField("m_EnvironmentFetcher",flags).GetValue(service);
            var build=(Task)fetcher.GetType().GetMethod("Build",flags).Invoke(fetcher,null); await build;
            var client=build.GetType().GetProperty("Result").GetValue(build);
            if(client==null) throw new InvalidOperationException("Cần đăng nhập Unity Editor để triển khai cấu hình test.");
            var generated=client.GetType().GetField("m_EnvironmentClient",flags).GetValue(client);
            var method=generated.GetType().GetMethod("UnityCreateEnvironmentAsync");
            var requestType=method.GetParameters()[0].ParameterType;
            var bodyType=requestType.GetConstructors().First().GetParameters()[1].ParameterType;
            var body=Activator.CreateInstance(bodyType,new object[]{"pickleverse-test"});
            var request=Activator.CreateInstance(requestType,new object[]{CloudProjectSettings.projectId,body});
            await (Task)method.Invoke(generated,new[]{request,null});
        }
    }
}
#endif
