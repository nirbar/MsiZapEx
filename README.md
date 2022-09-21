MsiZapEx is a command line utility and .NET assembly that enumerates Windows Installer products and WiX bundles and optionally deletes all their Windows Installer and WiX entries from the registry

- Note: The utility does not delete files or any other of the products' or bundles' contents. Rather, it deletes all indications of the installation by Windows Installer and WiX.
- Note: This utility is intended for use by Windows Installer and WiX bundle developers to recover from un-uninstallable packages. End users are encouraged not to use this tool.

# Command line interface

- --upgrade-code _UUID_: List all Windows Installer products for the given UpgradeCode
- --product-code _UUID_: Detect Windows Installer product for the given ProductCode
- --component-code _UUID_: Detect Windows Installer products for the given ComponentCode
- --bundle-upgrade-code _UUID_: List all WiX bundles for the given bundle UpgradeCode
- --bundle-product-code _UUID_: Detect WiX bundle for the given bundle Id (AKA ProductCode)
- --delete: Delete all WiX and Windows Installer entries for the provided UUID. Note that if multiple bundles or products are detected for a given UpgradeCode then you must delete each ProductCode separately
- --dry-run: May be specified with --delete only. Print all WiX and Windows Installer entries for the provided UUID that would be deleted
- --obfuscated: For a Windows Installer ProductCode or UpgradeCode or ComponentCode, the UUID is provided in its obfuscated form
- --verbose: Print each registry modification

# Open Issues

- Enumerate and delete user-level WiX bundles
- Create .reg file to undo changes