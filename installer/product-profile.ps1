function Get-ProductProfile([string]$Flavor) {
    switch ($Flavor) {
        'NeoSnap' {
            return [ordered]@{
                Flavor = 'NeoSnap'; Name = 'Neo Snap'; Version = '0.1.21'
                InstallFolder = 'SnapCraft'; DataFolder = 'SnapCraft'
                StartupValue = 'NeoSnap'; UninstallKey = 'SnapCraft'
                InstanceId = '37B63537-46B0-4D51-A9C0-619BE83FE608'
                Language = 'th'; IconName = 'NeoSnap-0.1.21.ico'
                OutputFile = 'Neo-Snap-Windows-Setup-0.1.21.exe'
                PublishFolder = 'publish-NeoSnap-0.1.21'; StagingFolder = 'setup-staging-NeoSnap-0.1.21'
            }
        }
        'Snapzy' {
            return [ordered]@{
                Flavor = 'Snapzy'; Name = 'SnapZy'; Version = '1.0.1'
                InstallFolder = 'Snapzy'; DataFolder = 'Snapzy'
                StartupValue = 'Snapzy'; UninstallKey = 'Snapzy'
                InstanceId = '6A2D75A0-73EE-4B83-9D4F-C3E14C7D5C10'
                Language = 'en'; IconName = 'Snapzy-1.0.1-A.ico'
                OutputFile = 'SnapZy-Setup-1.0.1.exe'
                PublishFolder = 'publish-Snapzy-1.0.1'; StagingFolder = 'setup-staging-Snapzy-1.0.1'
            }
        }
        default { throw "Unsupported ProductFlavor '$Flavor'. Choose NeoSnap or Snapzy." }
    }
}
