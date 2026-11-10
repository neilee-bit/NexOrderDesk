# NEX ORDER DESK – online releases with GitHub

How it fits together:

```
Directory.Build.props  ──►  version shown in the app, MSI ProductVersion, update.json
git tag v2.7.1  ──push──►  GitHub Actions (.github/workflows/release.yml)
                              └─ tools/build-release.ps1  → 2 MSIs + 2 one-click setups + install.ps1
                                                             + SHA256SUMS.txt + update.json
                              └─ GitHub Release v2.7.1 (draft → you press Publish)
Installed app  ──HTTPS──►  github.com/<owner>/<repo>/releases/latest/download/update.json
                              └─ newer? download MSI → check size + SHA256 + MSI signature
                              └─ app closes → Windows Installer (UAC prompt) → app restarts
```

| File | What it is |
|---|---|
| `Directory.Build.props` | **The one place** for version, repository, minimum supported version |
| `tools/build-release.ps1` | Builds both MSIs, validates them, writes `update.json` + `SHA256SUMS.txt` |
| `build_release.bat` / `build_msi.bat` | Run that script on your PC |
| `.github/workflows/release.yml` | Runs the same script on GitHub when a tag like `v2.7.1` is pushed |
| `.github/workflows/build.yml` | **Test build** on every push to a branch: builds everything, installs it on a clean Windows PC, starts the app, upgrades, uninstalls. No release is made. |
| `src/NexOrderDesk.Setup` | The one-click setup program (like the Chrome installer) |
| `tools/sign.ps1` | Optional code signing – does nothing until a certificate is configured |
| `release-notes/vX.Y.Z.md` | Release notes for each version (`TEMPLATE.md` to start from) |
| `installer/Package.wxs` | The MSI (WiX 5). **Never change its UpgradeCode.** |

What a release contains:

| Asset | Purpose |
|---|---|
| `NexOrderDesk_X.Y.Z_Setup.msi` | Light installer (2–4 MB, needs the free .NET 8 Desktop Runtime x64) |
| `NexOrderDesk_X.Y.Z_Setup_Full.msi` | Full installer (60–70 MB, needs nothing) |
| **`NexOrderDesk_Setup.exe`** | **One-click setup – give this to people.** Small; downloads the right installer, installs, starts the app |
| `NexOrderDesk_Setup_Offline.exe` | Same one-click setup with the full installer inside – for PCs without internet |
| `install.ps1` | Install from PowerShell with one line (no SmartScreen warning, see below) |
| `update.json` | Manifest: version, release date, download URLs, SHA256, size, notes, minimum supported version |
| `SHA256SUMS.txt` | Checksums for people who download by hand |

Each app updates with the same kind of installer it was installed with (light → light, full → full).

**Permanent download links** (always the newest published version – put them in a bookmark or message):

* Setup: `https://github.com/neilee-bit/NexOrderDesk/releases/latest/download/NexOrderDesk_Setup.exe`
* Offline setup: `https://github.com/neilee-bit/NexOrderDesk/releases/latest/download/NexOrderDesk_Setup_Offline.exe`
* PowerShell: `irm https://github.com/neilee-bit/NexOrderDesk/releases/latest/download/install.ps1 | iex`

### The one-click setup

Double-click → Windows asks for permission once (UAC) → one small window: *Downloading… → Installing… →
NEX ORDER DESK is installed* → the app starts by itself. No wizard pages, no questions.

* Picks the **small** installer when the PC already has the .NET 8 Desktop Runtime, otherwise the **complete** one.
* Downloads only from this repository over HTTPS and checks size, file type and SHA256 before installing.
* Upgrades an installed version in place (same folder), keeps all settings and orders.
* If NEX ORDER DESK is open it waits ("Please close NEX ORDER DESK to continue") and continues by itself.
* Errors show a plain message with **Try again** and **Show details** (log: `%TEMP%\NexOrderDesk_Setup\install.log`).
* For IT: `NexOrderDesk_Setup.exe /auto /nolaunch` installs without any questions (exit code 0 = OK).
* The MSI files still work as before (with the wizard) for anyone who prefers them.

---

## Why the first online release is 2.7.0 (not 2.6.0)

2.6.0 and older do not contain the update checker, so they can never update themselves.
**2.7.0 is the first version with online updates.** PCs that have 2.6.0 installed just install the
2.7.0 MSI once by hand – it replaces 2.6.0 automatically (same installer identity) and keeps all
settings. From then on every PC updates itself.

---

## A. One-time GitHub setup (about 15 minutes)

1. **Create a GitHub account** (if you do not have one) at https://github.com.
2. **Create the repository**: GitHub → **New repository** → name `NexOrderDesk`.
   * **Public** – simplest. The installed apps can download updates with no password.
   * **Private source code?** The installed apps cannot download from a private repository
     (no passwords are ever stored in the app). Then create a second, **public** repository
     (e.g. `NexOrderDesk-Releases`, with just a README) and see step 6.
3. **Put the code on GitHub** (install *Git for Windows* or *GitHub Desktop*). In the `NexOrderDesk 2.7` folder:
   ```
   git init
   git add .
   git commit -m "NEX ORDER DESK 2.7.0"
   git branch -M main
   git remote add origin https://github.com/neilee-bit/NexOrderDesk.git
   git push -u origin main
   ```
   (With GitHub Desktop: *File → Add local repository → Publish repository*.)
4. **Set the repository in `Directory.Build.props`** (only used for builds on your PC – GitHub fills it in by itself):
   ```xml
   <UpdateRepository>neilee-bit/NexOrderDesk</UpdateRepository>
   ```
   Commit and push this change.
5. **Allow the workflow to create releases**: repository → **Settings → Actions → General →
   Workflow permissions → Read and write permissions → Save**.
6. *(Only for private source + public releases repo)* Settings → **Secrets and variables → Actions**:
   * Variable `RELEASES_REPO` = `neilee-bit/NexOrderDesk-Releases`
   * Secret `RELEASES_TOKEN` = a fine-grained personal access token with **Contents: Read and write**
     on the releases repository only. (It stays on GitHub; it is never put in the app.)
7. *(Optional)* Variable `AUTO_PUBLISH` = `true` to publish releases immediately instead of as drafts.
   Leave it unset for now – drafts let you check everything first.

## B. First official release – 2.7.0

1. Check `Directory.Build.props` says `<Version>2.7.0</Version>` and `release-notes/v2.7.0.md` reads well.
2. *(Recommended)* Test on your PC first: double-click **`build_release.bat`**. The `release` folder
   must contain both MSIs, both setups, `install.ps1`, `update.json` and `SHA256SUMS.txt`. Install the light MSI and open the app:
   the title shows **NEX ORDER DESK 2.7.0**.
3. Create and push the tag:
   ```
   git tag v2.7.0
   git push origin v2.7.0
   ```
   (GitHub Desktop: *History → right-click the commit → Create Tag… v2.7.0*, then *Push origin*.)
4. GitHub → **Actions** → watch **Release** (about 5–10 minutes). Green tick = done.
   If it fails, open the red step and send me the error lines.
5. GitHub → **Releases** → **Draft: NEX ORDER DESK 2.7.0**. Check that it has 7 files
   (2 MSIs, `NexOrderDesk_Setup.exe`, `NexOrderDesk_Setup_Offline.exe`, `install.ps1`, `update.json`,
   `SHA256SUMS.txt`) and the notes. Press **Edit → Publish release**.
6. Check the manifest is reachable – open in a browser:
   `https://github.com/neilee-bit/NexOrderDesk/releases/latest/download/update.json`
   It must show `"version": "2.7.0"`.
7. Install on every PC with `NexOrderDesk_Setup.exe` (or the PowerShell line) – it upgrades 2.6 automatically.
   In the app click **⟳ Check for updates** → *“You are using the latest version.”*

## C. Every future update (e.g. 2.7.1 or 2.8.0)

1. Make the changes in the code.
2. In `Directory.Build.props` set `<Version>2.7.1</Version>`
   (`MAJOR.MINOR.PATCH`: fixes → PATCH, new features → MINOR, big changes → MAJOR).
3. Copy `release-notes/TEMPLATE.md` to `release-notes/v2.7.1.md` and write what is new.
4. Commit, push, then tag and push the tag:
   ```
   git commit -am "2.7.1"
   git push
   git tag v2.7.1
   git push origin v2.7.1
   ```
5. Check the draft on GitHub → **Publish release**. The next time each PC starts the app it shows
   **“⬆ Update 2.7.1 available”**; **Check for updates** shows it immediately.

* The tag decides the version on GitHub, so app, MSI, tag and `update.json` always match.
* `MinimumSupportedVersion` in `Directory.Build.props`: raise it only if an old version must not update
  directly (those PCs are then told to install by hand from the release page).
* Never delete or re-use a published version number; publish a new PATCH version instead.
* A broken release? Publish a fixed higher version, or edit the release to *Draft* to hide it.

## D. Test: an older installed version updates to a newer one

Do this once after setup (use a spare PC or your own):

1. Publish **2.7.0** (section B) and install `NexOrderDesk_2.7.0_Setup.msi`.
   Open the app, create an order type or add a material, change a colour – so there is user data to keep.
2. Make the test release **2.7.1**: set `<Version>2.7.1</Version>`, the notes `release-notes/v2.7.1.md`
   already exist, commit, `git tag v2.7.1`, `git push origin v2.7.1`, publish the draft.
3. Start NEX ORDER DESK 2.7.0. After a few seconds **“⬆ Update 2.7.1 available”** appears in the top bar
   (or click **⟳ Check for updates**).
4. The update window shows installed **2.7.0**, new **2.7.1**, release date and what's new.
   Click **Download and Install Update** → progress bar → *“downloaded and verified”*.
5. Click **Install now and restart** → confirm → the app closes → Windows asks for permission (UAC) →
   a small installer progress window → NEX ORDER DESK starts again.
6. Check:
   * the title says **NEX ORDER DESK 2.7.1**
   * your order type / material / colour from step 1 are still there
   * **Settings → Apps** lists **one** NEX ORDER DESK, version 2.7.1 (no duplicate)
   * **⟳ Check for updates** → *“You are using the latest version.”*
7. Offline test: disconnect the internet, start the app – it works normally; **Check for updates**
   says GitHub could not be reached.
8. Security test (optional): the app refuses any installer whose SHA256 does not match `update.json`
   (the file is deleted and nothing is run).

If an install fails, the previous version stays installed and the log is at
`%LocalAppData%\NexOrderDesk\updates\install.log`.

## Windows SmartScreen ("Windows protected your PC")

That blue window appears because the setup is **not digitally signed**. Windows shows it for every unsigned
program that was **downloaded with a web browser**. It is not an error in NEX ORDER DESK, and no code change can
switch it off – only a code-signing certificate can.

**What already avoids it (no cost):**

| Way of installing | SmartScreen warning? |
|---|---|
| In-app updates (⟳ Check for updates) | **Never** – the app downloads the update itself |
| PowerShell one-liner `irm …/install.ps1 \| iex` | **No** – nothing is downloaded by a browser |
| `NexOrderDesk_Setup.exe` downloaded in a browser | **Once** per PC: *More info → Run anyway*. The setup then downloads the installer itself, so there is no second warning |
| Copy the setup from a USB stick / network share | Usually no (only browser downloads are marked) |
| A downloaded file: right-click → **Properties → Unblock** → OK, then run | No |

So: install once per PC with the PowerShell line (or *Run anyway*), and every later version arrives through the
in-app update without any warning.

**To remove the warning completely – sign the files** (the build is ready for it, see `tools/sign.ps1`):

1. Get a code-signing certificate:
   * **Commercial OV certificate** (Sectigo, Certum, DigiCert, SSL.com …) issued to Promotees Group, typically a
     few hundred US$ a year. Since 2023 these come on a USB token or a cloud signing service, not as a file.
     Windows trusts signed files immediately, and SmartScreen stops warning once the certificate has some
     download history (usually a few weeks).
   * **Microsoft Azure Artifact Signing** (about US$10/month) – currently only for organisations in the
     USA, Canada, EU and UK and individuals in the USA/Canada, so probably not available from Sri Lanka yet.
   * **SignPath Foundation** – free, but only for open-source projects (public code under an open-source licence).
2. Configure signing on GitHub: **Settings → Secrets and variables → Actions → New repository secret**
   * a certificate file: `SIGN_PFX_BASE64` (the .pfx as base64) + `SIGN_PFX_PASSWORD`, **or**
   * a cloud signing tool: `SIGN_COMMAND` (the command that signs one file; the file path is added at the end).
3. Release as usual. Every exe and msi is then signed before the checksums are made.

## Where data lives (never touched by installing, updating or uninstalling)

* Settings, option lists, order types, icons: `%LocalAppData%\NexOrderDesk`
* Saved order PDFs: `Documents\NexOrderDesk Orders`
* Program: `C:\Program Files\NEX ORDER DESK` (replaced by updates)
