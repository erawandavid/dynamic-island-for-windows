# Pulau — Dynamic Island untuk Windows · Rencana Implementasi

> **Untuk Claude Code.** Dokumen ini adalah sumber kebenaran proyek. "Pulau" hanya nama kerja — ganti dengan cari-ganti `Pulau` kalau mau nama lain.
> Repo: publik di GitHub, lisensi MIT, supaya orang bisa clone/build atau langsung download dari Releases.

---

## 0. Aturan kerja untuk Claude Code

1. Kerjakan **satu fase per permintaan**, urut dari Fase 0. Jangan mulai fase berikutnya sebelum diminta.
2. Di awal fase: baca ulang bagian fase itu dan `CLAUDE.md`.
3. Di akhir fase, wajib:
   - `dotnet build -c Release` dan `dotnet test` lulus tanpa error,
   - update `CHANGELOG.md` (bagian *Unreleased*),
   - centang checklist fase itu di file ini,
   - commit dengan format Conventional Commits (`feat:`, `fix:`, `docs:`, `chore:` …). **Jangan push** kecuali diminta.
4. Kamu tidak bisa melihat layar. Untuk tiap fase yang menyentuh UI, tulis langkah uji manual di `docs/MANUAL_TESTS.md`, minta saya menjalankannya, lalu perbaiki berdasarkan laporan saya.
5. Keputusan kecil yang tidak dijawab dokumen ini: ambil default yang masuk akal dan catat di `docs/DECISIONS.md`. Keputusan besar/sulit dibalik: tanya dulu.
6. Bahasa: kode, komentar, pesan commit, README, dan semua docs publik dalam **bahasa Inggris** (supaya kontributor internasional bisa ikut). Ngobrol dengan saya boleh Bahasa Indonesia.
7. Paket NuGet ditambahkan dengan `dotnet add package <Nama>` (otomatis versi stable terbaru). Jangan pakai versi prerelease.
8. **Jangan menyalin kode dari proyek Dynamic Island lain.** Beberapa berlisensi GPL-3.0 / CC BY-SA yang tidak kompatibel dengan MIT. Ide fitur boleh, kode tidak.

---

## 1. Produk

Pill hitam kecil di tengah atas layar yang:
- **Idle:** pill kosong kecil, tidak mengganggu.
- **Compact:** melebar sedikit saat ada aktivitas (musik diputar, volume berubah, charger dicolok).
- **Expanded:** membesar saat di-hover/diklik — kontrol media, jam/tanggal/baterai.
- **Hidden:** otomatis sembunyi saat game/video fullscreen.

Target pengguna: Windows 10 (1809+) dan Windows 11, x64 & ARM64. Download zip dari Releases → extract → jalan. Tanpa install .NET, tanpa admin.

### Bukan tujuan v1 (non-goals)
- Tidak menyembunyikan/menggantikan OSD volume bawaan Windows.
- Tidak ada akun, cloud, telemetri, atau akses jaringan sama sekali.
- Tidak inject ke proses lain atau game.
- Tidak membaca notifikasi Windows (masuk Fase 7, eksperimen).
- Tidak cross-platform.

---

## 2. Keputusan teknis

| Aspek | Pilihan | Alasan |
|---|---|---|
| Runtime | **.NET 10 (LTS)**, C# terbaru | LTS, didukung sampai Nov 2028 |
| UI | **WPF** | Window borderless + transparan per-piksel + topmost sudah matang. WinUI 3 masih repot untuk window transparan tanpa frame; Electron terlalu berat untuk app yang nyala terus |
| MVVM | `CommunityToolkit.Mvvm` | Source generator, ringan |
| DI | `Microsoft.Extensions.DependencyInjection` | Standar |
| Win32 interop | `Microsoft.Windows.CsWin32` | P/Invoke di-generate dari `NativeMethods.txt`, tidak tulis manual |
| Media | `Dubya.WindowsMediaController` (wrapper SMTC) | Akses Spotify, browser, dll. lewat API resmi Windows |
| Volume | `NAudio` (`NAudio.CoreAudioApi`) | Event perubahan volume master |
| Baterai | `Windows.System.Power.PowerManager` (WinRT) | Event colok/cabut & persentase |
| Tray | `H.NotifyIcon.Wpf` | Tray icon native WPF |
| Logging | `Serilog` + `Serilog.Sinks.File` | Log ke file untuk debugging user |
| Test | xUnit + `Microsoft.Extensions.TimeProvider.Testing` | `FakeTimeProvider` untuk test logika berbasis waktu |
| TFM | `net10.0-windows10.0.19041.0`, `SupportedOSPlatformVersion` = `10.0.17763.0` | Perlu proyeksi WinRT; SMTC butuh Win10 1809+ |
| Rilis | self-contained, single-file, win-x64 + win-arm64 | User tidak perlu install apa-apa |

---

## 3. Arsitektur

```
┌────────────────────────── Pulau.App (WPF) ───────────────────────────┐
│  IslandWindow ── IslandViewModel ◄── ActivityManager ◄── Modules     │
│       │               │                (Core)            ├ Media     │
│  IslandAnimator   IslandStateMachine                     ├ Volume    │
│  (springs, Core)  (Core)                                 ├ Battery   │
│                                                          └ ...       │
│  TrayIcon · SettingsWindow · FullscreenWatcher · Interop (CsWin32)   │
└──────────────────────────────────────────────────────────────────────┘
Pulau.Core  = logika murni (tanpa WPF/WinRT), target net10.0, 100% unit-testable
```

### Struktur repo

```
pulau/
├─ src/
│  ├─ Pulau.Core/            # abstraksi modul, ActivityManager, StateMachine, Spring, model Settings
│  └─ Pulau.App/             # WPF: entry point, windows, views, tray, DI root
│     ├─ Modules/            # MediaModule, VolumeModule, BatteryModule (+ ViewModel & View)
│     ├─ Interop/            # NativeMethods.txt + helper Win32
│     ├─ Views/              # IslandWindow, SettingsWindow, DataTemplates
│     └─ Assets/             # ikon .ico original (bukan aset Apple)
├─ tests/
│  └─ Pulau.Core.Tests/
├─ docs/
│  ├─ ARCHITECTURE.md
│  ├─ ADDING_A_MODULE.md     # panduan kontributor bikin modul baru
│  ├─ DECISIONS.md
│  └─ MANUAL_TESTS.md
├─ .github/
│  ├─ workflows/ci.yml
│  ├─ workflows/release.yml
│  ├─ ISSUE_TEMPLATE/bug_report.yml, feature_request.yml
│  └─ pull_request_template.md
├─ global.json · Directory.Build.props · .editorconfig · .gitignore · .gitattributes
├─ README.md · LICENSE · CONTRIBUTING.md · CHANGELOG.md · CLAUDE.md
└─ <solution file>
```

### 3.1 State machine

State: `Hidden`, `Idle`, `Compact`, `Expanded`.

| Dari | Pemicu | Ke |
|---|---|---|
| Idle | ada activity | Compact |
| Compact | semua activity hilang | Idle |
| Idle / Compact | hover ≥ 150 ms, klik, atau hotkey | Expanded |
| Expanded | mouse keluar > 400 ms | Compact (jika ada activity) / Idle |
| Compact | lagu berganti & setting *auto-peek* aktif | Expanded selama 3 dtk → kembali |
| Semua | fullscreen terdeteksi / user hide lewat tray/hotkey | Hidden |
| Hidden | kondisi hilang / user show | state sebelumnya |

- Saat **Expanded**, activity *transient* (mis. volume) diabaikan kecuali prioritas `Critical`.
- Expanded tanpa activity = panel **Home**: jam, tanggal (ikut format sistem), persentase baterai jika laptop.

### 3.2 Activity & prioritas

```csharp
public enum ActivityPriority { Low, Normal, High, Critical }
public enum ActivityKind { Persistent, Transient }

public sealed record IslandActivity(
    string ModuleId,
    ActivityPriority Priority,
    ActivityKind Kind,
    TimeSpan? Duration,          // wajib untuk Transient
    object CompactViewModel,     // dipetakan ke View lewat DataTemplate di App
    object? ExpandedViewModel);

public interface IIslandModule
{
    string Id { get; }
    string DisplayName { get; }
    Task StartAsync(CancellationToken ct);
    Task StopAsync();
    event EventHandler<IslandActivity?> ActivityChanged;  // null = activity dihapus
}
```

Aturan `ActivityManager`:
- Yang tampil = prioritas tertinggi; seri → yang paling baru.
- Transient menimpa persistent dengan prioritas ≤ dirinya selama `Duration`, lalu persistent kembali tampil.
- Semua logika waktu memakai `TimeProvider` (bisa di-fake di test).
- Modul yang melempar exception diisolasi: di-log, dinonaktifkan, app tetap jalan.

### 3.3 Animasi

- Fisika **spring** sendiri di Core (`Spring.Step(dt)`), bukan easing bawaan WPF, supaya morph terasa "hidup" seperti iPhone. Parameter: stiffness + damping ratio (mulai dari damping ratio ~0.8, sedikit bouncy; disetel manual).
- Properti yang dianimasikan: `Width`, `Height`, `CornerRadius`, plus opacity/scale konten.
- Loop render via `CompositionTarget.Rendering` **hanya saat ada spring yang belum diam** — saat diam, tidak ada loop (CPU ≈ 0%).
- Retarget di tengah animasi mempertahankan kecepatan; `dt` di-clamp untuk mencegah lompatan saat frame tersendat.
- Transisi konten: konten lama fade-out cepat (~100 ms) → bentuk morph → konten baru fade-in menjelang akhir.
- Hormati pengaturan Windows "Animation effects" (`UISettings.AnimationsEnabled`): kalau mati, pakai transisi instan/pendek.

Ukuran awal (DIP, nanti bisa diskala lewat settings):

| State | Ukuran | Radius |
|---|---|---|
| Idle | 120 × 32 | 16 |
| Compact | 220–280 × 34 (tergantung konten) | 17 |
| Expanded (Home) | 340 × 110 | 28 |
| Expanded (Media) | 380 × 140 | 28 |

Gaya visual: latar `#000000`, teks putih, teks sekunder `#A1A1A1`, font Segoe UI Variable, jarak 8 DIP dari tepi atas work area. Tema gelap saja di v1.

### 3.4 Pendekatan window

Satu window transparan berukuran tetap (≥ ukuran expanded terbesar + margin bayangan, mis. 480 × 220 DIP) di tengah atas monitor; pill adalah `Border` di dalamnya. Piksel yang sepenuhnya transparan harus **tembus klik** — verifikasi di Fase 1. Jika ternyata tidak tembus, fallback: ubah ukuran window mengikuti bounds pill, atau toggle `WS_EX_TRANSPARENT` berdasarkan posisi kursor. Catat hasilnya di `DECISIONS.md`.

---

## 4. Fase

### Fase 0 — Fondasi repo
- [x] `global.json` mengunci SDK 10.0.x (`rollForward: latestFeature`)
- [x] Solution + `Pulau.Core`, `Pulau.App`, `Pulau.Core.Tests`
- [x] `Directory.Build.props`: `Nullable` enable, `ImplicitUsings` enable, `TreatWarningsAsErrors` di Release
- [x] `.gitignore` (`dotnet new gitignore`), `.editorconfig`, `.gitattributes` (`* text=auto`)
- [x] `LICENSE` MIT — Copyright (c) 2026 David Dylan Erawan
- [x] `README.md` kerangka (EN): nama, tagline, placeholder screenshot/GIF, fitur, download, build from source, privacy, contributing, license
- [x] `CLAUDE.md` ringkas: perintah build/test/run, struktur folder, aturan dari Bagian 0
- [x] `CONTRIBUTING.md`, `CHANGELOG.md` (format Keep a Changelog), `docs/ARCHITECTURE.md` (dari Bagian 3), `docs/DECISIONS.md`, `docs/MANUAL_TESTS.md`
- [x] `.github/workflows/ci.yml`: push & PR ke `main`, `windows-latest`, setup-dotnet dari `global.json`, restore → build Release → test
- [x] Issue template (YAML form) bug & feature, PR template

**Selesai jika:** build & test lulus lokal, app kosong bisa dijalankan, CI hijau setelah saya push.

### Fase 1 — Window island
- [ ] `app.manifest`: DPI awareness PerMonitorV2, supportedOS Win10/11
- [ ] `IslandWindow`: `WindowStyle=None`, `AllowsTransparency=True`, `Topmost`, `ShowInTaskbar=False`, `ShowActivated=False`, `ResizeMode=NoResize`
- [ ] Extended style via CsWin32: `WS_EX_TOOLWINDOW` (tidak muncul di Alt+Tab) + `WS_EX_NOACTIVATE` (tidak mencuri fokus)
- [ ] Posisi tengah atas **work area** monitor terpilih; hitung ulang saat display berubah, DPI berubah, dan saat Explorer restart (pesan `TaskbarCreated`)
- [ ] Tegaskan ulang topmost saat jendela foreground berganti (`SetWinEventHook` `EVENT_SYSTEM_FOREGROUND`), bukan polling cepat
- [ ] Verifikasi area transparan tembus klik (lihat 3.4)
- [ ] Single instance (named `Mutex`; peluncuran kedua langsung keluar)
- [ ] Tray icon: Show/Hide island, Settings (placeholder, disabled), Open logs folder, Quit
- [ ] Ikon `.ico` original sederhana (pill), dibuat sendiri
- [ ] Serilog ke `%LOCALAPPDATA%\Pulau\logs`, rolling harian, simpan 7 file
- [ ] Handler global untuk unhandled exception (Dispatcher, AppDomain, TaskScheduler) → log, jangan crash diam-diam

**Selesai jika (uji manual):** pill muncul di tengah atas; tidak ada di taskbar & Alt+Tab; mengklik pill tidak mengambil fokus dari app yang sedang dipakai mengetik; area di sekitar pill bisa diklik tembus; posisi tepat di scaling 100%/125%/150% dan di multi monitor; Quit menutup bersih; menjalankan dua kali tetap satu instance.

### Fase 2 — Animasi & state machine
- [ ] Core: `Spring` + unit test (konvergen ke target, settle, retarget mempertahankan kecepatan, clamp `dt`)
- [ ] Core: `IslandStateMachine` + unit test untuk setiap baris tabel 3.1
- [ ] App: `IslandAnimator` mengikat spring ke Width/Height/CornerRadius; berhenti saat diam
- [ ] Transisi konten (fade/scale) sesuai 3.3
- [ ] Timer hover/leave, klik untuk toggle expanded
- [ ] Panel Home (jam, tanggal, baterai)
- [ ] Menu tray **Debug** (hanya `#if DEBUG`) untuk memaksa state Idle/Compact/Expanded dengan konten dummy

**Selesai jika:** morph halus tanpa patah (juga di layar refresh-rate tinggi); CPU ≈ 0% saat diam (cek Task Manager); tidak ada loop render saat tidak beranimasi.

### Fase 3 — Sistem modul + Media
- [ ] Core: `IIslandModule`, `IslandActivity`, `ActivityManager` + unit test dengan `FakeTimeProvider`
- [ ] App: registrasi modul lewat DI; `DataTemplate` memetakan tipe ViewModel → View
- [ ] `docs/ADDING_A_MODULE.md`: panduan langkah demi langkah bikin modul baru (contoh modul minimal)
- [ ] `MediaModule`:
  - pilih session SMTC aktif; jika banyak, utamakan yang sedang *Playing*
  - **Compact:** album art (sudut membulat) kiri, equalizer bar beranimasi kanan (bergerak hanya saat Playing)
  - **Expanded:** art, judul (ellipsis), artis, nama app sumber, tombol prev / play-pause / next, progress bar + waktu (interpolasi lokal di antara update; sembunyikan bar jika app tidak memberi timeline)
  - warna aksen diambil dari album art (rata-rata/dominan, brightness di-clamp) untuk bar & progress
  - aturan activity: Playing → Persistent/Normal; Paused → bertahan 5 menit lalu hilang; session ditutup → hilang; ganti lagu → auto-peek
  - semua callback SMTC di-marshal ke UI thread; baca thumbnail async dengan timeout; thumbnail kosong → ikon placeholder

**Selesai jika (uji manual):** Spotify desktop dan YouTube di Chrome/Edge menampilkan judul/artis/art yang benar; tombol berfungsi; ganti lagu ter-update < 1 dtk; menutup app menghapus activity; tidak crash saat thumbnail kosong.

### Fase 4 — Modul sistem
- [ ] `VolumeModule`: event volume device output default (NAudio); ikut pindah saat device default berganti (mis. colok headset); Transient/High 1,5 dtk; ikon speaker + bar level + status mute; debounce. OSD Windows tetap muncul — dokumentasikan sebagai *known behavior*
- [ ] `BatteryModule`: colok/cabut charger → Transient (animasi petir + %); baterai ≤ 20% dan ≤ 10% → Transient/High sekali per ambang; PC tanpa baterai → modul nonaktif
- [ ] `FullscreenWatcher`: `SHQueryUserNotificationState` (D3D fullscreen, presentation, busy) + cek jendela foreground menutupi seluruh monitor (kecualikan desktop/shell); cek saat foreground berganti + timer 1 dtk → state Hidden

**Selesai jika:** ubah volume dengan tombol keyboard → pill muncul ±1,5 dtk; colok/cabut charger → animasi muncul; game/video fullscreen → island hilang dan kembali saat keluar; pindah ke headset tetap terdeteksi.

### Fase 5 — Pengaturan & persistensi
- [ ] Model settings di Core; JSON (System.Text.Json source-gen) di `%APPDATA%\Pulau\settings.json`; ada nomor versi skema + migrasi; file rusak → backup lalu pakai default
- [ ] `SettingsWindow` (gelap, sederhana; window biasa yang muncul di taskbar saat terbuka):
  - **General:** start with Windows, hide in fullscreen, pilihan monitor (primary / tertentu), offset vertikal, skala ukuran 80–130%
  - **Behavior:** delay hover, auto-peek saat ganti lagu, timeout media paused
  - **Modules:** toggle tiap modul
  - **Media exclusions:** daftar app yang tidak boleh memicu island
  - **Hotkey:** toggle island (default `Ctrl+Alt+I`); jika `RegisterHotKey` gagal karena bentrok, beri tahu user untuk memilih kombinasi lain
  - **About:** versi, link GitHub, buka folder log
- [ ] Autostart lewat `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` (path exe ber-quote; diperbarui jika exe dipindah; dihapus saat dimatikan)
- [ ] Perubahan diterapkan langsung tanpa restart

**Selesai jika:** semua opsi tersimpan setelah restart; app tetap jalan normal saat `settings.json` dihapus atau dirusak.

### Fase 6 — Rilis publik
- [ ] Publish: `dotnet publish src/Pulau.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true` (ulang untuk `win-arm64`). **Jangan aktifkan trimming** (tidak didukung WPF)
- [ ] Versi diambil dari tag git (workflow mengoper `-p:Version=`)
- [ ] `.github/workflows/release.yml`: trigger push tag `v*` → test → publish x64 & arm64 → zip `Pulau-vX.Y.Z-win-x64.zip` / `-win-arm64.zip` → file SHA256 → `gh release create` dengan release notes dari CHANGELOG + assets. `permissions: contents: write`
- [ ] README final: GIF demo (saya rekam), cara download, catatan SmartScreen (app belum di-sign → "More info → Run anyway"), persyaratan sistem, cara uninstall (matikan autostart, hapus folder + `%APPDATA%\Pulau`), build from source, FAQ (kenapa OSD volume Windows masih muncul, kenapa belum ada notifikasi), pernyataan privasi (tidak ada data yang keluar dari PC)
- [ ] Tag `v0.1.0`
- [ ] Opsional: installer Inno Setup, manifest winget, code signing (cek program signing gratis untuk open source)

**Selesai jika:** zip dari Releases bisa di-extract dan dijalankan di PC lain yang tidak punya .NET.

### Fase 7 — Eksperimen (setelah v0.1, satu per satu, masing-masing di branch sendiri)
- **Notifikasi** via `UserNotificationListener` — butuh *package identity* (sparse package / MSIX) + izin user; distribusinya lebih rumit karena butuh signing. Jadikan varian build opsional.
- Modul Timer / Stopwatch / Pomodoro (cocok dilabeli *good first issue*)
- Perangkat Bluetooth tersambung + baterainya
- Tema terang / efek acrylic
- Lokalisasi (en, id)
- API plugin (load modul eksternal) — perlu kajian keamanan dulu

---

## 5. Target non-fungsional
- CPU idle ≈ 0%; RAM < 150 MB (laporkan angka sebenarnya di README)
- Startup < 2 dtk
- Tanpa hak admin, tanpa jaringan, tanpa telemetri
- Kegagalan satu modul tidak menjatuhkan app
- Tombol punya `AutomationProperties.Name` (screen reader)

## 6. Risiko & catatan teknis
- **Klik tembus pada window transparan** — harus diverifikasi di Fase 1 (lihat 3.4).
- **Rebutan topmost** dengan jendela topmost lain (Task Manager "always on top", Start menu). Island tertutup Start menu = dapat diterima.
- **Keanehan SMTC:** sebagian app tidak memberi timeline/thumbnail; browser melapor per tab; thumbnail Spotify kadang telat.
- **OSD volume dobel** dengan OSD Windows — diterima di v1, didokumentasikan.
- **False positive fullscreen** (mis. browser F11, app borderless) — masuk akal untuk disembunyikan; sediakan toggle.
- **SmartScreen** pada exe tanpa signature — didokumentasikan di README.

## 7. Alur kerja GitHub (untuk saya, pemilik repo)
1. Install: Git, .NET 10 SDK, Visual Studio terbaru (workload *.NET desktop development*) atau VS Code + C# Dev Kit, GitHub CLI (opsional), Claude Code.
2. Buat repo publik `pulau` di GitHub → clone ke laptop Windows.
3. Taruh `PLAN.md` ini di root repo → jalankan Claude Code di folder repo → minta Fase 0.
4. Tiap fase di branch sendiri (`feat/phase-1-window`, dst.) → uji manual → push → buka PR ke `main` → CI hijau → merge.
5. Rilis: `git tag v0.1.0` → `git push origin v0.1.0` → GitHub Actions membuat Release berisi zip.
6. Setelah rilis: aktifkan Issues & Discussions, beri label `good first issue` pada ide Fase 7, pasang topik repo (`windows`, `dynamic-island`, `wpf`, `dotnet`).

## 8. Referensi (inspirasi fitur saja — jangan salin kode)
- DynamicWin (FlorianButz) — repo hanya berisi rilis, lisensi CC BY-SA 4.0
- WinIsland — Rust, GPL-3.0
- Quick Pill — Electron, MIT
- Windowsland (FrigonTech)
- DubyaDude/WindowsMediaController — contoh pemakaian wrapper SMTC, termasuk sampel WPF
- Microsoft Learn: `GlobalSystemMediaTransportControlsSessionManager`
