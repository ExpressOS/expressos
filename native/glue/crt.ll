; ModuleID = 'user-rt.c'
target datalayout = "e-p:32:32:32-i1:8:8-i8:8:8-i16:16:16-i32:32:32-i64:32:64-f32:32:32-f64:32:64-v64:64:64-v128:128:128-a0:0:64-f80:32:32-n8:16:32-S128"
target triple = "i386-unknown-linux-gnu"

%ExpressOS.Kernel.Arch.BootParam = type opaque
%"System.Byte[]" = type opaque

define i32 @csharp_main(%ExpressOS.Kernel.Arch.BootParam*) nounwind {
  tail call void @"ExpressOS.Kernel.Arch.Startup..Start.ExpressOS.Kernel.Arch.BootParam*"(%ExpressOS.Kernel.Arch.BootParam* %0)
  ret i32 0
}

define void @sfs_calculate_hmac(i32, i32, %"System.Byte[]"*, i8*) nounwind {
  tail call void @"ExpressOS.Kernel.SecureFSInode..CalculateHMAC.System.Int32.System.UInt32.System.Byte[].System.Byte*"(i32 %0, i32 %1, %"System.Byte[]"* %2, i8* %3)
  ret void
}

declare void @"ExpressOS.Kernel.Arch.Startup..Start.ExpressOS.Kernel.Arch.BootParam*"(%ExpressOS.Kernel.Arch.BootParam*)

declare void @"ExpressOS.Kernel.SecureFSInode..CalculateHMAC.System.Int32.System.UInt32.System.Byte[].System.Byte*"(i32, i32, %"System.Byte[]"*, i8*) 