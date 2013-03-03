namespace ExpressOS.Kernel
{
    public class AndroidApplicationInfo
    {
        public string PackageName;
        public string TaskAffinity { get { return PackageName; } }
        public string ProcessName { get { return PackageName; } }
        public int uid;
        public int flags;
        public string SourceDir;
        public string PublicSourceDir { get { return SourceDir; } }
        public string DataDir;
        public bool Enabled;
        public int TargetSdkVersion;
        public string Intent;

        public byte[] ToParcel()
        {
            var p = new Parcel();
            p.AddLengthString16(PackageName);
            p.AddLengthString16(ProcessName);
            p.AddLengthString16(TaskAffinity);
            p.AddLengthInt32(uid);
            p.AddLengthInt32(flags);
            p.AddLengthString16(SourceDir);
            p.AddLengthString16(PublicSourceDir);
            p.AddLengthString16(DataDir);
            p.AddLengthInt32(Enabled ? 1 : 0);
            p.AddLengthInt32(TargetSdkVersion);
            p.AddLengthString16(Intent);
            
            p.AllocateBuffer();

            p.WriteString16(PackageName);
            p.WriteString16(ProcessName);
            p.WriteString16(TaskAffinity);
            p.WriteInt32(uid);
            p.WriteInt32(flags);
            p.WriteString16(SourceDir);
            p.WriteString16(PublicSourceDir);
            p.WriteString16(DataDir);
            p.WriteInt32(Enabled ? 1 : 0);
            p.WriteInt32(TargetSdkVersion);
            p.WriteString16(Intent);
            return p.Buffer;
        }
    }
}
