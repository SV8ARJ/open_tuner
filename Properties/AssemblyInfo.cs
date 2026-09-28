using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

// TargetFramework is net10.0-windows, but GenerateAssemblyInfo=false (this file is hand-written,
// not SDK-generated) means the SDK's usual auto-generated [SupportedOSPlatform] attribute for that
// TFM is missing too - without it, the CA1416 platform-compatibility analyzer can't tell that every
// WinForms/GDI+ call in this Windows-only app is fine, and flags all ~2600 of them individually.
[assembly: SupportedOSPlatform("windows")]

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("Open Tuner")]
[assembly: AssemblyDescription("Frontend for Minitiouner Variants")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Amateur Radio - ZR6TG")]
[assembly: AssemblyProduct("Open Tuner")]
[assembly: AssemblyCopyright("Copyright ©  2023 - Tom Van den Bon")]
[assembly: AssemblyTrademark("Open Tuner")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(true)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("67e11ee1-c5c9-4394-a35e-899be0bb45dc")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
