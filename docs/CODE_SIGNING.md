# Code Signing Guide

Code signing your executable removes the "Unknown Publisher" warning in Windows SmartScreen.

## Options for Code Signing Certificates

### 1. Free Options

**Self-Signed Certificate (Development Only)**
- Free but users will still see warnings
- Good for testing the signing process

```powershell
# Create self-signed cert
New-SelfSignedCertificate -Type CodeSigningCert -Subject "CN=DownloadSorter" -CertStoreLocation Cert:\CurrentUser\My

# Export to PFX
$cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq "CN=DownloadSorter" }
$password = ConvertTo-SecureString -String "YourPassword" -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath ".\DownloadSorter.pfx" -Password $password
```

### 2. Paid Options (Removes SmartScreen Warning)

| Provider | Price/Year | Notes |
|----------|------------|-------|
| [SignPath](https://signpath.io) | Free for OSS | Best for open source |
| [Certum](https://www.certum.eu) | ~$50 | Cheapest paid option |
| [Comodo/Sectigo](https://sectigo.com) | ~$80 | Well-known |
| [DigiCert](https://digicert.com) | ~$400 | Enterprise |

### 3. Azure SignTool (CI/CD Friendly)

For GitHub Actions, use Azure Key Vault to store certificates securely.

## Signing Process

### Manual Signing

```powershell
cd build
.\sign-windows.ps1 -CertPath "path\to\cert.pfx" -CertPassword "password"
```

### GitHub Actions Signing

Add these secrets to your repository:
- `SIGNING_CERT_BASE64`: Base64-encoded PFX file
- `SIGNING_CERT_PASSWORD`: Certificate password

Then update `.github/workflows/ci.yml`:

```yaml
- name: Decode certificate
  run: |
    $bytes = [Convert]::FromBase64String("${{ secrets.SIGNING_CERT_BASE64 }}")
    [IO.File]::WriteAllBytes("cert.pfx", $bytes)

- name: Sign executable
  run: |
    $signtool = Get-ChildItem -Path "C:\Program Files (x86)\Windows Kits" -Recurse -Filter "signtool.exe" |
      Where-Object { $_.FullName -match "x64" } | Select-Object -First 1 -ExpandProperty FullName
    & $signtool sign /f cert.pfx /p "${{ secrets.SIGNING_CERT_PASSWORD }}" /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 ./publish/sorter.exe
```

## Verification

After signing, verify with:

```powershell
# Check signature
signtool verify /pa /v .\publish\sorter.exe

# Or in PowerShell
Get-AuthenticodeSignature .\publish\sorter.exe
```

## SmartScreen Reputation

Even with a valid certificate, new apps may show warnings until they build reputation.

**To build reputation faster:**
1. Submit to Microsoft for analysis: https://www.microsoft.com/wdsi/filesubmission
2. Distribute through trusted channels
3. Wait for users to download (reputation builds automatically)

## Signing the Installer

The InnoSetup installer can also be signed:

```powershell
# After building installer
.\sign-windows.ps1 -ExePath "..\publish\DownloadSorter-Setup-1.0.0.exe"
```
