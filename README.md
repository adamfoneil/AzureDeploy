# AzureDeploy (AzDeploy.exe)

This came from a need to deliver updated installers automatically for something I'm working on. I wanted it so that any time I built the project and a version number of any of the project components increased, a new installer would automatically build and upload to blob storage. I also had a need for extensibility on the kinds of installer deliverables I could generate. For example, I needed both .zip files generated as well as proprietary installer output from Just Great Software's [DeployMaster](https://www.deploymaster.com/).

There are four components of my solution: 

- **AzDeployUI.exe**, a WinForms app for setting up and testing your deployment parameters. Use this to generate an XML file which is passed as an argument to the console app AzDeploy.exe. See [CloudDeployUI](https://github.com/adamosoftware/AzureDeploy/tree/master/CloudDeployUI)

- **AzDeploy.exe**, a console app used in your project's post-build event. It requires an XML file name as an argument. See [AoDeliver](https://github.com/adamosoftware/AzureDeploy/tree/master/AoDeliver)

- **AzDeployLib.dll**, the deployment logic shared by both the UI and console app. See [BlobDeliveryLib](https://github.com/adamosoftware/AzureDeploy/tree/master/BlobDeliveryLib)

- **AzDeployClient.dll**, a client-side library applications can use to determine if a new version of an app is available. Install this via Nuget package **AzDeployClient**.

To use AzDeploy, you would follow these general steps:

1. Use AzDeployUI to generate a deployment script. Set all the properties and save. (Since your script will have an Azure storage account key, you will probably want to git to ignore it. Several times I have accidentally checked in sensitive info to GitHub, and I've had to regenerate keys.)

2. In AzDeployUI, click "Add to Project" to attach the AzDeploy.exe command to a project's post-build event. You can also do it manually in Visual Studio. "Add to Project" works in VS2015, but not 2017 yet.

3. As you develop and build project, any time you increment a version number of the project or any constituent part of it, AzDeploy should build a new installer and upload it to blob storage.

4. If you want your end user application to be able to detect and download later versions of itself, install package **AzDeployClient**.

Use its `InstallManager` class like this:

    var im = new InstallManager(*storage account*, *container*, *installerExe*, *productName*);
    await im.AutoInstallAsync();

The `installerExe` should be the case-sensitive same value in your [Engine.InstallerOutput](https://github.com/adamosoftware/AzureDeploy/blob/master/BlobDeliveryLib/Engine.cs#L79)
