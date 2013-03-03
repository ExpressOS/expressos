using ExpressOS.Kernel;

namespace ExpressOS.Kernel.Arch
{
    public static class Startup
    {
        public static void Start(ref BootParam param)
        {
            Console.WriteLine("Hello from ExpressOS-Managed");

            ArchGlobals.Initialize(ref param);
            Globals.Initialize(ref param);

            SyscallProfiler.Initialize();

            Misc.Initialize();
            FileSystem.Initialize();
            AESManaged.Initialize();
            SHA1Managed.Initialize();

            AndroidApplicationInfo appInfo = new AndroidApplicationInfo();
            var appName = "me.haohui.expressos.browserbench";
            appInfo.PackageName = appName;
            appInfo.uid = 1002;
            appInfo.flags = 0x8be45;
            appInfo.SourceDir = "/system/app/BrowserBench.apk";
            appInfo.DataDir = "/data/data/" + appName;
            appInfo.Enabled = true;
            appInfo.TargetSdkVersion = 10;
            appInfo.Intent = appName + "/" + appName + ".BrowserActivity";

#if false
            var argv = new ASCIIString[] {
                new ASCIIString("/system/bin/simple-hello"),
            };
            var envp = new ASCIIString[] {
                //new ASCIIString("LD_PRELOAD=/system/lib/libr2.so"),
                //new ASCIIString("HH_DEBUG=1"),
            };
#elif false
            var argv = new ASCIIString[] {
                new ASCIIString("/system/bin/bench-sqlite"),
                new ASCIIString("/data/data/com.valkyrie/1.db"),
            };
            var envp = new ASCIIString[] { };
#elif false
            var argv = new ASCIIString[] {
                new ASCIIString("/system/bin/bench-bootanim"),
            };
            var envp = new ASCIIString[] { 
                new ASCIIString("CLASSPATH=/system/framework/am.jar"),
                new ASCIIString("PATH=/sbin:/vendor/bin:/system/sbin:/system/bin:/system/xbin"),
                new ASCIIString("LD_LIBRARY_PATH=/vendor/lib:/system/lib"),
                new ASCIIString("ANDROID_BOOTLOGO=1"),
                new ASCIIString("ANDROID_ROOT=/system"),
                new ASCIIString("ANDROID_ASSETS=/system/app"),
                new ASCIIString("ANDROID_DATA=/data"),
                new ASCIIString("EXTERNAL_STORAGE=/mnt/sdcard"),
                new ASCIIString("ASEC_MOUNTPOINT=/mnt/asec"),
                new ASCIIString("LOOP_MOUNTPOINT=/mnt/obb"),
                new ASCIIString("BOOTCLASSPATH=/system/framework/core.jar:/system/framework/bouncycastle.jar:/system/framework/ext.jar:/system/framework/framework.jar:/system/framework/android.policy.jar:/system/framework/services.jar:/system/framework/core-junit.jar"),
                // new ASCIIString("LD_PRELOAD=/system/lib/libr2.so"),
            };
#elif false
            var argv = new ASCIIString[] {
                new ASCIIString("/data/presenter"),
            };
            var envp = new ASCIIString[] { 
                new ASCIIString("CLASSPATH=/system/framework/am.jar"),
                new ASCIIString("PATH=/sbin:/vendor/bin:/system/sbin:/system/bin:/system/xbin"),
                new ASCIIString("LD_LIBRARY_PATH=/vendor/lib:/system/lib"),
                new ASCIIString("ANDROID_BOOTLOGO=1"),
                new ASCIIString("ANDROID_ROOT=/system"),
                new ASCIIString("ANDROID_ASSETS=/system/app"),
                new ASCIIString("ANDROID_DATA=/data"),
                new ASCIIString("EXTERNAL_STORAGE=/mnt/sdcard"),
                new ASCIIString("ASEC_MOUNTPOINT=/mnt/asec"),
                new ASCIIString("LOOP_MOUNTPOINT=/mnt/obb"),
                new ASCIIString("BOOTCLASSPATH=/system/framework/core.jar:/system/framework/bouncycastle.jar:/system/framework/ext.jar:/system/framework/framework.jar:/system/framework/android.policy.jar:/system/framework/services.jar:/system/framework/core-junit.jar"),
                new ASCIIString("SLIDES=/data/slides.zip"),
            };
#elif false
            var argv = new ASCIIString[] {
                new ASCIIString("/system/bin/bench-vbinder"),
            };
            var envp = new ASCIIString[] { };
#elif false
            var argv = new ASCIIString[] {
                new ASCIIString("/system/xbin/wget"),
                new ASCIIString("http://128.174.236.238"),
            };
            var envp = new ASCIIString[] { 
            };
#elif false
            var argv = new ASCIIString[] {
                new ASCIIString("/system/bin/app_process"),
                new ASCIIString("/system/bin"),
                new ASCIIString("com.android.commands.am.Am"),
                new ASCIIString("start"),
                new ASCIIString("-a"),
                new ASCIIString("android.intent.action.MAIN"),
                new ASCIIString("-n"),
                new ASCIIString("com.valkyrie/com.valkyrie.HelloAndroidActivity"),
            };

            var envp = new ASCIIString[] {
                new ASCIIString("CLASSPATH=/system/framework/am.jar"),
                new ASCIIString("PATH=/sbin:/vendor/bin:/system/sbin:/system/bin:/system/xbin"),
                new ASCIIString("LD_LIBRARY_PATH=/vendor/lib:/system/lib"),
                new ASCIIString("ANDROID_BOOTLOGO=1"),
                new ASCIIString("ANDROID_ROOT=/system"),
                new ASCIIString("ANDROID_ASSETS=/system/app"),
                new ASCIIString("ANDROID_DATA=/data"),
                new ASCIIString("EXTERNAL_STORAGE=/mnt/sdcard"),
                new ASCIIString("ASEC_MOUNTPOINT=/mnt/asec"),
                new ASCIIString("LOOP_MOUNTPOINT=/mnt/obb"),
                new ASCIIString("BOOTCLASSPATH=/system/framework/core.jar:/system/framework/bouncycastle.jar:/system/framework/ext.jar:/system/framework/framework.jar:/system/framework/android.policy.jar:/system/framework/services.jar:/system/framework/core-junit.jar"),
                /*new ASCIIString("HH_DEBUG=1"), */
            };
#elif true
            var argv = new ASCIIString[] {
                new ASCIIString("/system/bin/app_process"),
                new ASCIIString("/system/bin"),
                new ASCIIString("android.app.ActivityThread"),
            };

            var envp = new ASCIIString[] {
                new ASCIIString("CLASSPATH=/system/framework/am.jar"),
                new ASCIIString("PATH=/sbin:/vendor/bin:/system/sbin:/system/bin:/system/xbin"),
                new ASCIIString("LD_LIBRARY_PATH=/vendor/lib:/system/lib"),
                new ASCIIString("ANDROID_BOOTLOGO=1"),
                new ASCIIString("ANDROID_ROOT=/system"),
                new ASCIIString("ANDROID_ASSETS=/system/app"),
                new ASCIIString("ANDROID_DATA=/data"),
                new ASCIIString("EXTERNAL_STORAGE=/mnt/sdcard"),
                new ASCIIString("ASEC_MOUNTPOINT=/mnt/asec"),
                new ASCIIString("LOOP_MOUNTPOINT=/mnt/obb"),
                new ASCIIString("BOOTCLASSPATH=/system/framework/core.jar:/system/framework/bouncycastle.jar:/system/framework/ext.jar:/system/framework/framework.jar:/system/framework/android.policy.jar:/system/framework/services.jar:/system/framework/core-junit.jar"),
                new ASCIIString("HH_DEBUG=1"),
                /* new ASCIIString("LD_PRELOAD=/libr2.so"), */
            };

#else
            var argv = new ASCIIString[] {
                new ASCIIString("/system/bin/app_process"),
                new ASCIIString("-Xgc:preverify"),
                new ASCIIString("-Xgc:postverify"),
                new ASCIIString("-Xgc:verifycardtable"),
                new ASCIIString("/system/bin"),
                new ASCIIString("android.os.GcTests"),
            };

            var envp = new ASCIIString[] {
                new ASCIIString("CLASSPATH=/system/framework/frameworkcoretests.jar"),
                new ASCIIString("PATH=/sbin:/vendor/bin:/system/sbin:/system/bin:/system/xbin"),
                new ASCIIString("LD_LIBRARY_PATH=/vendor/lib:/system/lib"),
                new ASCIIString("ANDROID_BOOTLOGO=1"),
                new ASCIIString("ANDROID_ROOT=/system"),
                new ASCIIString("ANDROID_ASSETS=/system/app"),
                new ASCIIString("ANDROID_DATA=/data"),
                new ASCIIString("EXTERNAL_STORAGE=/mnt/sdcard"),
                new ASCIIString("ASEC_MOUNTPOINT=/mnt/asec"),
                new ASCIIString("LOOP_MOUNTPOINT=/mnt/obb"),
                new ASCIIString("BOOTCLASSPATH=/system/framework/core.jar:/system/framework/bouncycastle.jar:/system/framework/ext.jar:/system/framework/framework.jar:/system/framework/android.policy.jar:/system/framework/services.jar:/system/framework/core-junit.jar"),
                new ASCIIString("HH_DEBUG=1"),
                //new ASCIIString("LD_PRELOAD=/libr2.so"),
            };
#endif
            var proc = ExpressOS.Kernel.Exec.CreateProcess(argv[0], argv, envp, appInfo);
            if (proc == null)
                Console.WriteLine("Cannot start init");

            Globals.SecurityManager.OnActiveProcessChanged(proc);

            Console.WriteLine("ExpressOS initialized");
            Looper.ServerLoop();
        }

    }
}
