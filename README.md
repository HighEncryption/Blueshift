# Blueshift

Blueshift as a desktop application for synchronizing (copying) files from one or more Microsoft OneDrive accounts to a local location (external drive, NAS, etc.). The purpose of Blueshift is to perform a one-way copy of an entire OneDrive, then keep those folder/files to-to-date with any changes made in OneDrive.

Why the name Blueshift?
> In physics, blueshift is the change in wavelength/energy caused by movement of a source towards the observer. Blueshift (this apps moves files from the cloud closer to you, so the name is fitting.

**Note: Blueshift is still in the early phases and much testing is still required. Please report issues you encounter so they can be fixed.**

# Installation

Blueshift should be installed on a machine that is running 24/7 so that it can continually sync changes as the occur in OneDrive. A virtual machine works great or any other computer running Windows 10+.

Currently, the installation is entirely manual. Eventually there will be PowerShell/MSI to make this easier, but for now it requires you to modify files directly.

1. Create a folder where the Blueshift binaries will run from. Example: C:\Apps
1. Copy the contents of the release into the Apps folder. You should have two sub-folders: `Blueshift` and `Blueshift.TokenBroker`
1. Go into the Blueshift folder and find the `config.json` file. This contains the settings used by Blueshift. Modify the file as follows:

    ```
    {
      "AppId": "e1e07cdb-c97b-4031-a74e-1781e0677adf",
      "SourceSections": "SourceUser1,SourceUser2",
      "SourceUser1": {
        "RootPath": "D:\\Backups\\OneDrive Backup\\user1@live.com",
        "UserPrincipalName": "user1@live.com"
      },
      "SourceUser2": {
        "RootPath": "D:\\Backups\\OneDrive Backup\\user2@gmail.com",
        "UserPrincipalName": "user2@gmail.com"
      }
    }
    ```

    1. Determine the set of OneDrive accounts you want to sync. For each account, create a section similar to SourceUser1 and SourceUser2.
        1. Replace `RootPath` with the full path to the directory where files from OneDrive will be copied to. This can be a local drive or a UNC share (aka Windows Shared Folder). **Note that backslash characters need to be escaped (two \\ characters)**
        1. Update the value of `UserPrincipalName` to contain the sign-in name of the account. In the example above, replace `user1@live.com` with your sign-in (aka email) address used to log into OneDrive
    1. For example OneDrive account added, also include the name of the section in the `SourceSections` shown above.

1. For the initial setup, you will need to authenticate as the user that owns the OneDrive account. This will allow Blueshift to cache an (encrypted) token that can be used to continuously pull changes. To do this, open an command prompt and do the following:

   `Blueshift.GraphTokenBroker.exe /getToken /path C:\ProgramData\Blueshift\SourceUser1\token.json`

   Note the following two requirements:
   - The part of the path `SourceUser1` needs to match the string used in the config file
   - The user principal name (aka email address) of the user that you login with needs to match the user specified in the config file. In this case, `user1@live.com`.

   Repeat this for each user added in the config.json file.

1. Blueshift is now ready to run. For the first run, it is best to use the command line and run it interactively, that way you can see any failures that might occur. Open a command prompt and run the following:

    `Blueshift.exe /sync`

    You should see a lot of logging showing that files are being copied.

1. Once the initial sync completes, create a scheduled task to run it every 15 minutes.