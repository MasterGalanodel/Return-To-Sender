using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace TD
{
    // Inactive during ordinary play. The standalone verification command explicitly opts in.
    public sealed class TDBuildSmoke : MonoBehaviour
    {
        [Serializable] private sealed class Report
        {
            public bool passed;
            public string unityVersion, mapSource, error, screenshot, screenshotError;
            public float elapsedSeconds;
            public int spawned, exited, killed, shots, hits, maxEnemies, maxProjectiles, errors;
        }
        private TDBootstrap bootstrap;
        private readonly Report report=new Report();
        private float started, duration=20;
        private string output;
        private bool attemptedCapture, finished;

        private void Start()
        {
            bootstrap=GetComponent<TDBootstrap>();
            string[] args=Environment.GetCommandLineArgs();
            output=Path.GetFullPath(Path.Combine(Application.dataPath,"..","TD-Smoke.json"));
            for(int i=0;i<args.Length-1;i++)
            {
                if(args[i]=="--hex-td-smoke-output") output=Path.GetFullPath(args[i+1]);
                if(args[i]=="--hex-td-smoke-seconds" && float.TryParse(args[i+1],NumberStyles.Float,CultureInfo.InvariantCulture,out float seconds))
                    duration=Mathf.Clamp(seconds,20,300);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            Application.runInBackground=true;
            Application.targetFrameRate=60;
            Application.logMessageReceived+=OnLog;
            started=Time.realtimeSinceStartup;
            foreach(int index in new [] {8,16,24}) bootstrap.Placement.TryPlace(bootstrap.Route[index],out _);
        }

        private void OnLog(string message,string trace,LogType type)
        {
            if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)
            {report.errors++;report.error=message;}
        }

        private void Update()
        {
            if(finished) return;
            var world=bootstrap.World;
            report.maxEnemies=Mathf.Max(report.maxEnemies,world.Enemies.Count);
            report.maxProjectiles=Mathf.Max(report.maxProjectiles,world.Projectiles.Count);
            float elapsed=Time.realtimeSinceStartup-started;
            if(!attemptedCapture && elapsed>10)
            {
                foreach(var projectile in world.Projectiles)
                {
                    Vector3 view=bootstrap.prototypeCamera.WorldToViewportPoint(projectile.transform.position);
                    if(view.x>0 && view.x<1 && view.y>0 && view.y<1) {Capture();break;}
                }
            }
            if(elapsed<duration) return;
            finished=true;
            if(!attemptedCapture) Capture();
            report.unityVersion=Application.unityVersion;
            report.mapSource=bootstrap.Map.SourceUsed;
            report.elapsedSeconds=elapsed;
            report.spawned=world.SpawnedCount;
            report.exited=world.ExitedCount;
            report.killed=world.KilledCount;
            report.shots=world.ShotsFired;
            report.hits=world.Hits;
            report.passed=bootstrap.Initialized && report.mapSource.StartsWith("packaged",StringComparison.Ordinal)
                && report.shots>0 && report.hits>0 && report.killed>0 && report.errors==0
                && report.maxEnemies<=64 && report.maxProjectiles<=64;
            File.WriteAllText(output,JsonUtility.ToJson(report,true));
            Application.Quit(report.passed ? 0 : 1);
        }

        private void Capture()
        {
            attemptedCapture=true;
            var camera=bootstrap.prototypeCamera;
            var previous=camera.targetTexture;
            Rect previousRect=camera.rect;
            var previousActive=RenderTexture.active;
            var target=new RenderTexture(1280,720,24);
            Texture2D pixels=null;
            try
            {
                camera.targetTexture=target;
                camera.rect=new Rect(0,0,1,1);
                camera.Render();
                RenderTexture.active=target;
                pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
                pixels.ReadPixels(new Rect(0,0,1280,720),0,0);
                pixels.Apply();
                report.screenshot=Path.ChangeExtension(output,".png");
                File.WriteAllBytes(report.screenshot,pixels.EncodeToPNG());
            }
            catch(Exception exception) {report.screenshotError=exception.Message;}
            finally
            {
                camera.targetTexture=previous;
                camera.rect=previousRect;
                RenderTexture.active=previousActive;
                target.Release();
                Destroy(target);
                if(pixels) Destroy(pixels);
            }
        }
        private void OnDestroy() {Application.logMessageReceived-=OnLog;}
    }
}
