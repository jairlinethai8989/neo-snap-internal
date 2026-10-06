function Get-ProductProfile([string]$Flavor) {
    switch ($Flavor) {
        'NeoSnap' {
            return [ordered]@{
                Flavor = 'NeoSnap'; Name = 'Neo Snap'; Version = '0.1.20'
                InstallFolder = 'SnapCraft'; DataFolder = 'SnapCraft'
                StartupValue = 'NeoSnap'; UninstallKey = 'SnapCraft'
                InstanceId = '37B63537-46B0-4D51-A9C0-619BE83FE608'
                Language = 'th'; IconName = 'NeoSnap-0.1.20.ico'
                OutputFile = 'Neo-Snap-Windows-Setup-0.1.20.exe'
                PublishFolder = 'publish-NeoSnap-0.1.20'; StagingFolder = 'setup-staging-NeoSnap-0.1.20'
            }
        }
        'Snapzy' {
            return [ordered]@{
                Flavor = 'Snapzy'; Name = 'Snapzy'; Version = '1.0.0'
                InstallFolder = 'Snapzy'; DataFolder = 'Snapzy'
                StartupValue = 'Snapzy'; UninstallKey = 'Snapzy'
                InstanceId = '6A2D75A0-73EE-4B83-9D4F-C3E14C7D5C10'
                Language = 'en'; IconName = 'Snapzy-1.0.0.ico'
                OutputFile = 'Snapzy-Setup-1.0.0.exe'
                PublishFolder = 'publish-Snapzy-1.0.0'; StagingFolder = 'setup-staging-Snapzy-1.0.0'
            }
        }
        default { throw "Unsupported ProductFlavor '$Flavor'. Choose NeoSnap or Snapzy." }
    }
}
