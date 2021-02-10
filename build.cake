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
// --verbose
//   Enables verbose messages.
// 
///////////////////////////////////////////////////////////////////////////////////////////////////

#addin nuget:?package=Cake.Json&version=5.2.0
#addin nuget:?package=Newtonsoft.Json&version=11.0.2

#load "build/build-state.cake"
#load "build/build-utilities.cake"

// Get the target that was specified.
var target = Argument("target", "Test");


///////////////////////////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////////////////////////


// Constructs the build state object.
Setup<BuildState>(context => {
    var state = new BuildState() {
        SolutionName = Argument("project", DefaultSolutionName),
        Target = target,
        Configuration = Argument("configuration", "Debug"),
        ContinuousIntegrationBuild = HasArgument("ci") || !BuildSystem.IsLocalBuild,
        Rebuild = HasArgument("rebuild"),
        SignOutput = HasArgument("sign-output"),
        Verbose = HasArgument("verbose")
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

    state.AssemblyVersion = $"{majorVersion}.{minorVersion}.0.0";
    state.AssemblyFileVersion = $"{majorVersion}.{minorVersion}.{patchVersion}.{buildCounter}";

    state.PackageVersion = string.IsNullOrWhiteSpace(versionSuffix) 
        ? $"{majorVersion}.{minorVersion}.{patchVersion}"
        : buildCounter > 0
            ? $"{majorVersion}.{minorVersion}.{patchVersion}-{versionSuffix}.{buildCounter}"
            : $"{majorVersion}.{minorVersion}.{patchVersion}-{versionSuffix}";

    state.BuildNumber = string.IsNullOrWhiteSpace(versionSuffix)
        ? $"{majorVersion}.{minorVersion}.{patchVersion}+{branch}.{buildCounter}"
        : $"{majorVersion}.{minorVersion}.{patchVersion}-{versionSuffix}+{branch}.{buildCounter}";

    state.InformationalVersion = string.IsNullOrWhiteSpace(buildMetadata)
        ? state.BuildNumber
        : $"{state.BuildNumber}#{buildMetadata}";

    if (!string.Equals(state.Target, "Clean", StringComparison.OrdinalIgnoreCase)) {
        BuildUtilities.SetBuildSystemBuildNumber(BuildSystem, state.BuildNumber);
        BuildUtilities.WriteBuildStateToLog(BuildSystem, state);
    }

    return state;
});


// Pre-task action.
TaskSetup(context => {
    BuildUtilities.WriteTaskStartMessage(BuildSystem, context.Task.Name);
});


// Post task action.
TaskTeardown(context => {
    BuildUtilities.WriteTaskEndMessage(BuildSystem, context.Task.Name);
});


///////////////////////////////////////////////////////////////////////////////////////////////////
// TASKS
///////////////////////////////////////////////////////////////////////////////////////////////////


// Cleans up artifact and bin folders.
Task("Clean")
    .WithCriteria<BuildState>((c, state) => state.Clean)
    .Does<BuildState>(state => {
        CleanDirectories($"./src/**/bin/{state.Configuration}");
        CleanDirectory($"./artifacts");
    });


// Restores NuGet packages.
Task("Restore")
    .Does<BuildState>(state => {
        DotNetCoreRestore(state.SolutionName);
    });


// Builds the solution.
Task("Build")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore")
    .Does<BuildState>(state => {
        var buildSettings = new DotNetCoreBuildSettings {
            Configuration = state.Configuration,
            MSBuildSettings = new DotNetCoreMSBuildSettings()
        };

        buildSettings.MSBuildSettings.Targets.Add(state.Rebuild ? "Rebuild" : "Build");
        BuildUtilities.ApplyMSBuildProperties(buildSettings.MSBuildSettings, state);
        DotNetCoreBuild(state.SolutionName, buildSettings);
    });


// Runs unit tests.
Task("Test")
    .IsDependentOn("Build")
    .Does<BuildState>(state => {
        DotNetCoreTest(state.SolutionName, new DotNetCoreTestSettings {
            Configuration = state.Configuration,
            NoBuild = true
        });
    });


// Builds NuGet packages.
Task("Pack")
    .IsDependentOn("Test")
    .Does<BuildState>(state => {
        var buildSettings = new DotNetCoreBuildSettings {
            Configuration = state.Configuration,
            NoRestore = true,
            MSBuildSettings = new DotNetCoreMSBuildSettings()
        };

        buildSettings.MSBuildSettings.Targets.Add("Pack");
        BuildUtilities.ApplyMSBuildProperties(buildSettings.MSBuildSettings, state);
        DotNetCoreBuild(state.SolutionName, buildSettings);
    });


///////////////////////////////////////////////////////////////////////////////////////////////////
// EXECUTION
///////////////////////////////////////////////////////////////////////////////////////////////////


RunTarget(target);
