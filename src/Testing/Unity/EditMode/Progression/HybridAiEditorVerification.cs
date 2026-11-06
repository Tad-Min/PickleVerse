using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

// Direct NUnit assertion verification preserves unsaved scenes in the running Editor.
public static class HybridAiEditorVerification
{
    public static Report RunDirect(string typeNames = "HybridAiTests,BotContactChanceTests")
    {
        // Invoke these pure NUnit assertions directly: TestRunnerApi's scene preparation can
        // prompt to save unrelated unsaved scenes, even for synchronous EditMode execution.
        var scene=SceneManager.GetActiveScene();
        var report=new Report {sceneBefore=scene.path,dirtyBefore=scene.isDirty};
        foreach(var name in typeNames.Split(','))
        {
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(name,false)).First(t=>t!=null);
            foreach(var method in type.GetMethods())
            {
                var cases=method.GetCustomAttributes(typeof(NUnit.Framework.TestCaseAttribute),true);
                if(cases.Length==0 && method.GetCustomAttributes(typeof(NUnit.Framework.TestAttribute),true).Length==0)continue;
                var arguments=new List<object[]>();
                foreach(NUnit.Framework.TestCaseAttribute item in cases)arguments.Add(item.Arguments);
                if(cases.Length==0)arguments.Add(Array.Empty<object>());
                foreach(var args in arguments)
                {
                    var item=new Case {name=type.Name+"."+method.Name+"("+string.Join(",",args)+")"};
                    try { method.Invoke(Activator.CreateInstance(type),args);item.state="Passed";report.passed++; }
                    catch(System.Reflection.TargetInvocationException error) {item.state="Failed";item.message=error.InnerException.ToString();report.failed++;}
                    report.cases.Add(item);
                }
            }
        }
        scene=SceneManager.GetActiveScene();report.sceneAfter=scene.path;report.dirtyAfter=scene.isDirty;
        var path=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/verification/hybrid-unity-direct.json"));
        Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,JsonUtility.ToJson(report,true));
        return report;
    }
    [Serializable] public sealed class Report
    {
        public int passed, failed, skipped;
        public string sceneBefore, sceneAfter;
        public bool dirtyBefore, dirtyAfter;
        public List<Case> cases = new List<Case>();
    }
    [Serializable] public sealed class Case { public string name, state, message; }
}
