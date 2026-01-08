# crestron-deployment-tool

## Features

- deploy to multiple devices at once
- update firmware without locking up your computer
	- after the SFTP transfer is complete, and the update command is sent, you can disconnect your computer, or leave it connected to monitor progress
- upload configuration files without filezilla
	- currently only supports uploading to \\user\\ folder

## Instructions

1. open application
1. select network interfaces connected to crestron devices
1. begin discovery
1. select devices to deploy
1. select deployment actions
	1. **you can select multiple deployment actions at once**
		1. selecting multiple deployment actions and multiple devices will re-prompt the desired devices per action
	1. **AVAILABLE DEPLOYMENT ACTIONS**
		1. provision new device
			1. this will assign the administrator account on a box that is freshly unboxed or restored
		1. set / update ip configuration
			1. enable / disable DHCP [and set static networking details]
		1. update firmware
		1. send / update configuration files
			1. sends any file to the \\user\\ folder on a crestron device
		1. send / update programming
		1. send / update user interfaces
			1. currently only supports true touchpanels
			1. adding uploading of files to processors is on the roadmap
		1. send console commands
			1. for commands that require a secondary confirmation, add the confirmation after the command
				1. i.e. for the restore command, type "restore y"
1. follow the wizards and prompts that are presented to you
1. click begin deployment after the deployment window is opened

## Known Issues

- setting dns servers may fire off errors
	- need to parse existing servers and delete before adding
- sending the sig file does not work properly