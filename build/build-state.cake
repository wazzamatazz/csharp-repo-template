// Class for sharing build state between Cake tasks.
public class BuildState {

    // The solution to build.
    public string SolutionName { get; set; }

    // The build number.
    public string BuildNumber { get; set; }

    // The Cake target.
    public string Target { get; set; }

    // The MSBuild configuration.
    public string Configuration { get; set; }

    // Specifies if this is a rebuild or an incremental build.
    public bool Rebuild { get; set; }

    // Specifies if artifacts and bin folders should be cleaned before building.
    public bool Clean => Rebuild || string.Equals(Target, "Clean", StringComparison.OrdinalIgnoreCase);

    // Specifies if this is a continuous integration build.
    public bool ContinuousIntegrationBuild { get; set; }

    // Specifies if DLLs and NuGet packages should be signed.
    public bool SignOutput { get; set; }

    // Specifies if output signing is allowed.
    public bool CanSignOutput => SignOutput && ContinuousIntegrationBuild;

    // MSBuild AssemblyVersion property value.
    public string AssemblyVersion { get; set; }

    // MSBuild AssemblyFileVersion property value.
    public string AssemblyFileVersion { get; set; }

    // MSBuild InformationalVersion property value.
    public string InformationalVersion { get; set; }

    // MSBuild Version property value.
    public string PackageVersion { get; set; }

    public bool Verbose { get; set; }

}
