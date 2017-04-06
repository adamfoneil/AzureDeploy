# AzureDeploy

I needed an easy way to deliver updated installers for something I'm working on. By "easy" I mean it needed to be no more than a post-build event on my Visual Studio project build. I didn't want to do anything in particular with MSBuild nor learn about/setup Continuous Integration in any fancy sense. I simply needed an installer exe to upload to blob storage any time I build my project and any newer-version components are present.

I'm using Just Great Software's [DeployMaster](https://www.deploymaster.com/) product to build my installer, so I needed a little bit of integration with that. That part I sort of winged since they don't offer any automation interface to speak of apart from a command line build. I needed to update the product's version number, and this required editing the install script directly.

I'll post more details shortly!
