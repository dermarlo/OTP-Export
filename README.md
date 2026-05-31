# OTP-Export

Decrypts and exports OTP secrets from [WinOTP Authenticator](https://github.com/VladimirAkopyan/Authenticator), a Windows Store 2FA app that has no built-in export function. The output is a plain JSON file containing the TOTP seeds for all configured accounts, which you can use to migrate to any other authenticator app.
It was created out of frustration during the migration of a Windows 10 installation to a new Windows 11 PC, where the user did not want to use OneDrive, which is the only sync-option WinOTP Authenticator provides. 

OTP-Export has no ties to WinOTP Authenticator and does not use any advanced techniques, it only uses public Windows APIs. As I do not have any experience with Windows or C# it is written with the help of Claude Code. The program itself is trivial and could probably be integrated as an export functionality in WinOTP Authenticator easily. I decided against this, as the repo seems inactive and I do not feel confident to implement the full functionality, given my lack of experience with the environment. 

## Requirements

- Windows 10 (build 17763) or later
- Must be run as the **same Windows user** who set up the Authenticator app on **the original Windows environment the app was used on**

## Installation

Download the latest release from the [Releases](../../releases) page. The exe is self-contained — no .NET installation required.

To build from source, install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) and run `dotnet run`.

## Usage

**Important: the app needs to run as the same user who set up and used the Authenticator App on the same Windows Environment that the app was used on**. You can not export the JSON file to another machine and decrypt it there, as the encryption uses the local user-scope (see below). 

**Auto-discover and export** (WinOTP must be installed):

```
OTP-Export.exe
```

**Export from a backed-up `Accounts.json`** (app no longer needs to be installed):

```
OTP-Export.exe --input "C:\path\to\Accounts.json"
```

**Write output to a specific location:**

```
OTP-Export.exe --output "D:\export\accounts-plain.json"
```

By default the output file `accounts-plain.json` is written to the current directory.

### Output format

```json
[
  { "Username": "you@example.com", "Secret": "JBSWY3DPEHPK3PXP", "Service": "Google" },
  ...
]
```

The `Secret` field is the Base32-encoded TOTP seed. To import into any 2FA app that supports `otpauth://` URIs:

```
otpauth://totp/<Service>:<Username>?secret=<Secret>&issuer=<Service>
```

## How it works

WinOTP stores its account database at:

```
%LOCALAPPDATA%\Packages\<Authenticator-package>\LocalState\Accounts.json
```

The file is encrypted with the [Windows Data Protection API](https://learn.microsoft.com/en-us/windows/win32/seccng/cng-dpapi) (DPAPI) in user scope, meaning the encryption key is derived from the Windows user's credentials and is only accessible to that user on that machine. This tool calls the WinRT `DataProtectionProvider.UnprotectAsync` API to decrypt the file, then writes the plaintext JSON to disk.
