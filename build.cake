const string DefaultSolutionName = "./RENAME-ME.sln";

///////////////////////////////////////////////////////////////////////////////////////////////////
// COMMAND LINE ARGUMENTS:
//
// --project=<PROJECT OR SOLUTION>
//   The MSBuild project or solution to build. Default: see DefaultSolutionName constant above.
//
// --target=<TARGET>
//   Specifies the Cake target to run. Default: Test
//
// --configuration=<CONFIGURATION>
//   Specifies the MSBuild configuration to use. Default: Debug
//
// --rebuild
//   Specifies if this is a rebuild rather than an incremental build. All artifact and bin folders 
//   will be cleaned prior to a rebuild.
//
// --ci
//   Forces continuous integration build mode. Not required if the build is being run by a 
//   supported continuous integration build system.
//
// --sign-output
//   Tells MSBuild that signing is required by setting the 'SignOutput' property to 'True'. The 
//   signing implementation must be supplied by MSBuild.
//
// --branch=<BRANCH>
//   The source control branch name that is being build. Default: master
//
// --build-counter=<COUNTER>
//   The build counter. This is used when generating version numbers for the build.
//
// --build-metadata=<METADATA>
//   Additional build metadata that will be included in the information version number generated 
//   for compiled assemblies.
// 
///////////////////////////////////////////////////////////////////////////////////////////////////

#addin nuget:?package=Cake.Json&version=5.2.0
#addin nuget:?package=Newtonsoft.Json&version=11.0.2

// Get the target that was specified.
var target = Argument("target", "Test");


///////////////////////////////////////////////////////////////////////////////////////////////////
// BUILD STATE TYPE DEFINITION
///////////////////////////////////////////////////////////////////////////////////////////////////


public class BuildData {

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


    // Adds MSBuild properties from the build data.
    public void ApplyMSBuildProperties(DotNetCoreMSBuildSettings settings) {
        // Specify if this is a CI build. 
        if (ContinuousIntegrationBuild) {
            settings.Properties["ContinuousIntegrationBuild"] = new List<string> { "True" };
        }

        // Specify if we are signing DLLs and NuGet packages.
        if (CanSignOutput) {
            settings.Properties["SignOutput"] = new List<string> { "True" };
        }

        // Set version numbers.
        settings.Properties["AssemblyVersion"] = new List<string> { AssemblyVersion };
        settings.Properties["AssemblyFileVersion"] = new List<string> { AssemblyFileVersion };
        settings.Properties["Version"] = new List<string> { PackageVersion };
        settings.Properties["InformationalVersion"] = new List<string> { InformationalVersion };
    }


    public void Dump() {
        Console.WriteLine();
        Console.WriteLine($"Solution Name: {SolutionName}");
        Console.WriteLine($"Build Number: {BuildNumber}");
        Console.WriteLine($"Target: {Target}");
        Console.WriteLine($"Configuration: {Configuration}");
        Console.WriteLine($"Rebuild: {Rebuild}");
        Console.WriteLine($"Continous Integration Build: {ContinuousIntegrationBuild}");
        Console.WriteLine($"Sign Output: {CanSignOutput}");
        Console.WriteLine();
        Console.WriteLine($"Informational Version: {InformationalVersion}");
        Console.WriteLine($"Assembly Version: {AssemblyVersion}");
        Console.WriteLine($"Assembly File Version: {AssemblyFileVersion}");
        Console.WriteLine($"Package Version: {PackageVersion}");
        Console.WriteLine();
    }

}


///////////////////////////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////////////////////////


// Constructs the build state object.
Setup<BuildData>(context => {
    var data = new BuildData() {
        SolutionName = Argument("project", DefaultSolutionName),
        Target = target,
        Configuration = Argument("configuration", "Debug"),
        ContinuousIntegrationBuild = HasArgument("ci") || !BuildSystem.IsLocalBuild,
        Rebuild = HasArgument("rebuild"),
        SignOutput = HasArgument("sign-output")
    };

    // Get raw version numbers from JSON.

    var versionJson = ParseJsonFromFile("./build/version.json");

    var majorVersion = versionJson.Value<int>("Major");
    var minorVersion = versionJson.Value<int>("Minor");
    var patchVersion = versionJson.Value<int>("Patch");
    var versionSuffix = versionJson.Value<string>("PreRelease");

    // Compute version numbers.

    var buildCounter = Argument("build-counter", 0);
    var buildMetadata = Argument("build-metadata", "");
    var branch = Argument("branch", "master");

    data.AssemblyVersion = $"{majorVersion}.{minorVersion}.0.0";
    data.AssemblyFileVersion = $"{majorVersion}.{minorVersion}.{patchVersion}.{buildCounter}";

    data.PackageVersion = string.IsNullOrWhiteSpace(versionSuffix) 
        ? $"{majorVersion}.{minorVersion}.{patchVersion}"
        : buildCounter > 0
            ? $"{majorVersion}.{minorVersion}.{patchVersion}-{versionSuffix}.{buildCounter}"
            : $"{majorVersion}.{minorVersion}.{patchVersion}-{versionSuffix}";

    data.BuildNumber = string.IsNullOrWhiteSpace(versionSuffix)
        ? $"{majorVersion}.{minorVersion}.{patchVersion}+{branch}.{buildCounter}"
        : $"{majorVersion}.{minorVersion}.{patchVersion}-{versionSuffix}+{branch}.{buildCounter}";

    data.InformationalVersion = string.IsNullOrWhiteSpace(buildMetadata)
        ? data.BuildNumber
        : $"{data.BuildNumber}#{buildMetadata}";

    // Tell TeamCity the build number if required.
    if (BuildSystem.IsRunningOnTeamCity) {
        BuildSystem.TeamCity.SetBuildNumber(data.BuildNumber);
    }

    data.Dump();

    return data;
});


// Pre-task action.
TaskSetup(context => {
    if (BuildSystem.IsRunningOnTeamCity) {
        BuildSystem.TeamCity.WriteStartProgress(context.Task.Name);
    }
});


// Post task action.
TaskTeardown(context => {
    if (BuildSystem.IsRunningOnTeamCity) {
        BuildSystem.TeamCity.WriteEndProgress(context.Task.Name);
    }
});


///////////////////////////////////////////////////////////////////////////////////////////////////
// TASKS
///////////////////////////////////////////////////////////////////////////////////////////////////


// Cleans up artifact and bin folders.
Task("Clean")
    .WithCriteria<BuildData>((c, data) => data.Clean)
    .Does<BuildData>(data => {
        CleanDirectories($"./src/**/bin/{data.Configuration}");
        CleanDirectory($"./artifacts");
    });


// Restores NuGet packages.
Task("Restore")
    .Does<BuildData>(data => {
        DotNetCoreRestore(data.SolutionName);
    });


// Builds the solution.
Task("Build")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore")
    .Does<BuildData>(data => {
        var buildSettings = new DotNetCoreBuildSettings {
            Configuration = data.Configuration,
            MSBuildSettings = new DotNetCoreMSBuildSettings()
        };

        buildSettings.MSBuildSettings.Targets.Add(data.Rebuild ? "Rebuild" : "Build");
        data.ApplyMSBuildProperties(buildSettings.MSBuildSettings);
        DotNetCoreBuild(data.SolutionName, buildSettings);
    });


// Runs unit tests.
Task("Test")
    .IsDependentOn("Build")
    .Does<BuildData>(data => {
        DotNetCoreTest(data.SolutionName, new DotNetCoreTestSettings {
            Configuration = data.Configuration,
            NoBuild = true
        });
    });


// Builds NuGet packages.
Task("Pack")
    .IsDependentOn("Test")
    .Does<BuildData>(data => {
        var buildSettings = new DotNetCoreBuildSettings {
            Configuration = data.Configuration,
            NoRestore = true,
            MSBuildSettings = new DotNetCoreMSBuildSettings()
        };

        buildSettings.MSBuildSettings.Targets.Add("Pack");
        data.ApplyMSBuildProperties(buildSettings.MSBuildSettings);
        DotNetCoreBuild(data.SolutionName, buildSettings);
    });


///////////////////////////////////////////////////////////////////////////////////////////////////
// EXECUTION
///////////////////////////////////////////////////////////////////////////////////////////////////


RunTarget(target);
