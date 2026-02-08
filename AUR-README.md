# AUR Package Maintenance

This directory contains files for the Arch User Repository (AUR) package.

## Files

- **PKGBUILD** - Build script for Arch Linux
- **.SRCINFO** - Package metadata (auto-generated)
- **you-tui.install** - Post-install/upgrade hooks

## Testing Locally

Before publishing to AUR, test the package locally:

```bash
# Validate PKGBUILD
namcap PKGBUILD

# Build the package
makepkg -s

# Validate built package
namcap you-tui-*.pkg.tar.zst

# Install locally for testing
makepkg -si

# Test the application
you-tui

# Uninstall after testing
sudo pacman -R you-tui
```

## Publishing to AUR

### Initial Setup

1. Create an AUR account at https://aur.archlinux.org/register
2. Configure SSH keys in your AUR account settings
3. Set up git config:
   ```bash
   git config --global user.name "Your Name"
   git config --global user.email "your@email.com"
   ```

### First Publish

```bash
# Clone the AUR repository
git clone ssh://aur@aur.archlinux.org/you-tui.git aur-you-tui
cd aur-you-tui

# Copy package files
cp ../PKGBUILD .
cp ../you-tui.install .

# Generate .SRCINFO
makepkg --printsrcinfo > .SRCINFO

# Review changes
git status
git diff

# Commit and push
git add PKGBUILD you-tui.install .SRCINFO
git commit -m "Initial import: you-tui 0.1.0"
git push
```

### Updating the Package

When releasing a new version:

1. Update version in PKGBUILD:
   ```bash
   pkgver=X.Y.Z
   pkgrel=1
   ```

2. Update checksums:
   ```bash
   # Download new source
   wget https://github.com/santiagoalmeida/you-tui/archive/vX.Y.Z.tar.gz
   
   # Calculate sha256sum
   sha256sum vX.Y.Z.tar.gz
   
   # Update sha256sums in PKGBUILD
   ```

3. Test build:
   ```bash
   makepkg -s
   ```

4. Update .SRCINFO:
   ```bash
   makepkg --printsrcinfo > .SRCINFO
   ```

5. Commit and push:
   ```bash
   cd aur-you-tui
   git add PKGBUILD .SRCINFO
   git commit -m "Update to version X.Y.Z"
   git push
   ```

## Creating a GitHub Release

Before updating AUR, create a GitHub release:

```bash
# Tag the release
git tag -a v0.1.0 -m "Release version 0.1.0"
git push origin v0.1.0

# Or use GitHub UI to create a release
# https://github.com/santiagoalmeida/you-tui/releases/new
```

## Notes

- Always use `sha256sums=('SKIP')` during development
- Calculate real checksums for AUR releases
- Test in a clean chroot: `extra-x86_64-build`
- Increment `pkgrel` for packaging-only changes
- Reset `pkgrel=1` when `pkgver` changes
- Follow [Arch Package Guidelines](https://wiki.archlinux.org/title/Arch_package_guidelines)

## Common Issues

**Build fails with .NET errors:**
- Ensure dotnet-sdk is installed
- Check .NET version: `dotnet --version`
- Clear package cache: `rm -rf ~/.nuget/packages`

**Package size too large:**
- This is expected for self-contained .NET apps (~60-80 MB)
- Runtime is included so users don't need .NET installed

**Missing dependencies:**
- Ensure all runtime deps are in `depends=()`: mpv, yt-dlp, fzf, socat
- Build deps go in `makedepends=()`: dotnet-sdk

## Support

For package-related issues:
- Comment on the AUR page
- Open an issue on GitHub

For application bugs:
- Open an issue on GitHub: https://github.com/santiagoalmeida/you-tui/issues
