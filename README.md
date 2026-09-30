# ExplorerKit

Werkzeugkasten für den Windows 11 Datei-Explorer:

- Ordner **als neuen Tab in einem bereits offenen Explorer-Fenster** öffnen – oder in einem neuen Fenster, falls keins offen ist
- bei mehreren Explorer-Fenstern das Zielfenster per **Auswahldialog** bestimmen
- offene Explorer-Fenster samt Tabs, Öffnungszeitpunkt und Laufzeit **auflisten** und **schließen**

| Projekt | Typ | Zweck |
|---|---|---|
| `ExplorerKit` | Klassenbibliothek (`SSP.ExplorerKit.dll`, NuGet `SSP.ExplorerKit`) | Wiederverwendbare Funktionalität inkl. Auswahldialog |
| `ExplorerKitSample` | Console App (`OpenInExplorer.exe`) | Kommandozeilen-Werkzeug und Anwendungsbeispiel der Bibliothek |

## Voraussetzungen

- Windows 11 (22H2 oder neuer – Explorer mit Tabs)
- .NET 10 SDK

## Build

```powershell
dotnet build ExplorerKit.slnx -c Release
```

Die Exe liegt danach unter
`src\ExplorerKitSample\bin\Release\net10.0-windows10.0.22621.0\OpenInExplorer.exe`.

NuGet-Paket der Bibliothek erzeugen:

```powershell
dotnet pack src\ExplorerKit -c Release
```

## Klassenbibliothek `ExplorerKit`

### Einbinden

Per Projektverweis:

```powershell
dotnet add <DeinProjekt>.csproj reference <Pfad>\src\ExplorerKit\ExplorerKit.csproj
```

oder als NuGet-Paket `SSP.ExplorerKit` (siehe [Build](#build)).

Das nutzende Projekt muss auf `net10.0-windows10.0.22621.0` oder neuer zielen:

```xml
<TargetFramework>net10.0-windows10.0.22621.0</TargetFramework>
```

Die Bibliothek nutzt Windows Forms (für den Auswahldialog); die Framework-Referenz wird automatisch übernommen.
Sie verwendet die in .NET eingebaute COM-Interop und ist daher **nicht mit NativeAOT oder Trimming kompatibel**.
Achtung bei datei-basierten Apps (`dotnet run app.cs`): Diese aktivieren standardmäßig NativeAOT – dort
`#:property PublishAot=false` setzen, sonst schlägt jeder Aufruf mit `NotSupportedException` fehl.

### Schnellstart

```csharp
using SSP.ExplorerKit;

Explorer.OpenInteractive(@"C:\Temp");
```

`OpenInteractive` erledigt alles in einem Aufruf:

| Situation | Verhalten |
|---|---|
| Explorer läuft nicht / kein Explorer-Fenster offen | Explorer wird mit dem Pfad in einem neuen Fenster gestartet |
| Ein Explorer-Fenster offen | Neuer Tab in diesem Fenster |
| Mehrere Explorer-Fenster offen | [Auswahldialog](#auswahldialog) – der Benutzer wählt das Fenster |

### Weitere Beispiele

```csharp
using SSP.ExplorerKit;

// Ohne Dialog: zuletzt benutztes Fenster, sonst neues Fenster
Explorer.Open(@"C:\Temp");

// Bestimmtes Zielfenster
Explorer.Open(@"C:\Temp", TargetWindow.Index(2));
Explorer.Open(@"C:\Temp", TargetWindow.Handle(hwnd));
Explorer.Open(@"C:\Temp", TargetWindow.Match("Projekte"));
Explorer.Open(@"C:\Temp", TargetWindow.Interactive);        // entspricht OpenInteractive

// Eigene Auswahllogik (null => neues Fenster, OperationCanceledException => Cancelled)
Explorer.Open(@"C:\Temp", TargetWindow.Custom(windows =>
    windows.FirstOrDefault(w => w.Tabs.Count < 5)));

// Offene Fenster abfragen
foreach (ExplorerWindow window in Explorer.GetWindows())
{
    Console.WriteLine($"{window.Index}: {window.Title}, offen seit {ExplorerFormat.Age(window.Age)}");
    foreach (ExplorerTab tab in window.Tabs)
    {
        Console.WriteLine($"   {tab.Name} -> {tab.Location}");
    }
}

// Fenster schließen, die länger als einen Tag offen sind
foreach (ExplorerWindow window in Explorer.GetWindows().Where(w => w.Age > TimeSpan.FromDays(1)))
{
    Explorer.CloseWindow(window);
}
```

Ergebnis auswerten:

```csharp
OpenResult result = Explorer.OpenInteractive(@"C:\Temp");
switch (result.Mode)
{
    case OpenMode.NewTab:    /* Tab in result.Window geöffnet */ break;
    case OpenMode.NewWindow: /* ggf. result.FallbackReason prüfen */ break;
    case OpenMode.Cancelled: /* Benutzer hat den Dialog abgebrochen */ break;
}
```

### Auswahldialog

Erscheint bei `Explorer.OpenInteractive` bzw. `TargetWindow.Interactive`, wenn mehrere Explorer-Fenster offen sind.

- **Oben – Fenster:** Nr., Fenstertitel, Zeitpunkt des Öffnens, Laufzeit („Läuft seit“), Anzahl Tabs, Handle.
  Das zuletzt benutzte Fenster ist vorausgewählt; mit Strg/Umschalt lassen sich mehrere markieren.
- **Unten – Tabs der markierten Fenster:** ein Tab pro Zeile mit Fenster-Nr., Tab-Name und vollständigem Pfad.

| Aktion | Ergebnis |
|---|---|
| **Öffnen** / Enter / Doppelklick | Neuer Tab im gewählten Fenster (genau ein Fenster markiert) → `OpenMode.NewTab` |
| **In neuem Fenster** | Pfad in einem neuen Explorer-Fenster öffnen → `OpenMode.NewWindow` |
| **Fenster schließen** / Entf | Markierte Explorer-Fenster samt Tabs schließen (mit Sicherheitsabfrage); die Liste wird danach neu geladen |
| **Aktualisieren** / F5 | Liste der Explorer-Fenster neu einlesen |
| **Abbrechen** / Esc / Schließen | Nichts öffnen → `OpenMode.Cancelled` |

### API

| Typ / Member | Beschreibung |
|---|---|
| `Explorer.OpenInteractive(path)` | Öffnet `path` als Tab; bei mehreren Fenstern mit Auswahldialog (siehe [Schnellstart](#schnellstart)) |
| `Explorer.Open(path, target = null, fallbackToNewWindow = true)` | Öffnet `path` als Tab im Zielfenster (Standard: `TargetWindow.MostRecent`, kein Dialog) |
| `Explorer.GetWindows()` | Alle sichtbaren Explorer-Fenster, zuletzt benutztes zuerst |
| `Explorer.CloseWindow(window \| handle, timeout = 5 s)` | Schließt ein Explorer-Fenster samt Tabs regulär (`WM_CLOSE`); `false`, wenn es nach dem Timeout noch offen ist |
| `TargetWindow.MostRecent` | Zuletzt benutztes Fenster |
| `TargetWindow.Interactive` | Auswahldialog, falls mehrere Fenster offen sind |
| `TargetWindow.Index(int)` | Fenster nach Nummer (1-basiert, Z-Reihenfolge) |
| `TargetWindow.Handle(nint)` | Fenster nach Handle |
| `TargetWindow.Match(string)` | Erstes Fenster, dessen Titel oder Tab den Text enthält (ohne Groß-/Kleinschreibung) |
| `TargetWindow.Custom(Func<IReadOnlyList<ExplorerWindow>, ExplorerWindow?>)` | Eigene Auswahl |
| `ExplorerWindow` | `Index`, `Handle`, `Title`, `Tabs`, `OpenedAt`, `Age` |
| `ExplorerTab` | `Name` (Tab-Beschriftung), `Location` (Pfad bzw. Anzeigename bei virtuellen Orten wie „Dieser PC“) |
| `OpenResult` | `Path`, `Mode`, `Window`, `FallbackReason` |
| `OpenMode` | `NewTab`, `NewWindow`, `Cancelled` |
| `ExplorerFormat.OpenedAt(...)`, `ExplorerFormat.Age(...)` | Anzeigeformate wie im Dialog, z. B. `30.09.2026 08:23`, `2 Std. 05 Min.` |

**Exceptions:**

| Exception | Wann |
|---|---|
| `ArgumentException` | Pfad leer oder ungültig |
| `DirectoryNotFoundException` | Ordner existiert nicht |
| `ExplorerWindowNotFoundException` | `Open`: explizit angefordertes Fenster (`Index`, `Handle`, `Match`) nicht gefunden · `CloseWindow`: Handle ist kein offenes Explorer-Fenster |
| `InvalidOperationException` | Tab konnte nicht erstellt werden und `fallbackToNewWindow` ist `false` |

Ein abgebrochener Auswahldialog ist **keine** Exception, sondern `OpenMode.Cancelled`.

**Threading:** Shell-COM-Objekte und der Dialog benötigen einen STA-Thread. Ist der aufrufende Thread kein STA-Thread
(z. B. Hintergrund-Thread, `async`-Code, Dienst), führt die Bibliothek die Arbeit auf einem temporären STA-Thread aus.
Ein `Custom`-Selektor läuft dann ebenfalls auf diesem Thread.

## Console App `OpenInExplorer.exe`

```
OpenInExplorer <path> [target]
OpenInExplorer --list
```

| Option | Beschreibung |
|---|---|
| *(keine)* | `Explorer.OpenInteractive`: ein Fenster offen → neuer Tab darin, mehrere offen → Auswahldialog |
| `--no-dialog` | Kein Dialog – neuer Tab im zuletzt benutzten Explorer-Fenster |
| `--window <n>` | Neuer Tab in Fenster Nr. `n` laut `--list` (1 = zuletzt benutzt) |
| `--hwnd <handle>` | Neuer Tab im Fenster mit diesem Handle (dezimal oder `0x…` hex) |
| `--match <text>` | Neuer Tab im ersten Fenster, dessen Titel oder einer seiner Tabs `<text>` enthält |
| `--list` | Offene Explorer-Fenster mit Nummer, Handle, Öffnungszeitpunkt und Tabs anzeigen |
| `-h`, `--help` | Hilfe anzeigen |

Ist kein Explorer-Fenster offen, wird der Pfad in einem neuen Fenster geöffnet.
`--no-dialog`, `--window`, `--hwnd` und `--match` schließen sich gegenseitig aus.
Umgebungsvariablen (`%USERPROFILE%`) werden aufgelöst; bei einem Dateipfad wird der enthaltende Ordner geöffnet.

### Beispiele

```powershell
OpenInExplorer C:\Temp                  # bei mehreren Fenstern: Auswahldialog
OpenInExplorer C:\Temp --no-dialog      # ohne Rückfrage ins zuletzt benutzte Fenster
OpenInExplorer "%USERPROFILE%\Downloads" --window 2
OpenInExplorer C:\Temp --match Projekte

OpenInExplorer --list
# [1] 0x1814BC  Fonts und 2 weitere Registerkarten – Explorer
#       Geöffnet: 30.09.2026 08:23 (läuft seit < 1 Min.)
#       - C:\Windows\Web
#       - C:\Program Files
#       - C:\Windows\Fonts
# [2] 0x7313A8  bin – Explorer
#       Geöffnet: 30.09.2026 07:57 (läuft seit 26 Min.)
#       - C:\mnt\bin
```

> Die Fensternummern entsprechen der Z-Reihenfolge und ändern sich, sobald ein anderes Fenster aktiviert wird.
> Für Skripte ist `--hwnd` daher zuverlässiger – das Handle bleibt stabil, solange das Fenster offen ist.

### Exit-Codes

| Code | Bedeutung |
|---|---|
| `0` | Erfolgreich geöffnet (Tab oder neues Fenster) |
| `1` | Ungültiger Aufruf oder ungültiger Pfad |
| `2` | Ordner existiert nicht |
| `3` | Angegebenes Explorer-Fenster nicht gefunden (es wird **kein** neues Fenster geöffnet) |
| `4` | Auswahldialog abgebrochen |

## Funktionsweise

Windows 11 bietet keine offizielle API für Explorer-Tabs. Die Bibliothek geht deshalb so vor:

1. Explorer-Fenster (`CabinetWClass`) per `EnumWindows` in Z-Reihenfolge ermitteln.
2. Dem aktiven Tab (`ShellTabWindowClass`) des Zielfensters `WM_COMMAND 0xA21B` senden –
   derselbe Befehl, den Explorer bei **Strg+T** ausführt.
3. Den neu entstandenen Tab über `Shell.Application.Windows()` finden
   (Zuordnung per `IServiceProvider` → `IShellBrowser.GetWindow`) und mit `Navigate2` zum Pfad navigieren.
4. Klappt das nicht innerhalb von 5 Sekunden, wird (sofern erlaubt) ein neues Fenster geöffnet.

**Zeitpunkt des Öffnens:** Alle Explorer-Fenster laufen im selben `explorer.exe`-Prozess, dessen Startzeit daher
nichts über das einzelne Fenster aussagt. Explorer betreibt aber jedes Fenster auf einem eigenen UI-Thread;
`OpenedAt` ist die Startzeit dieses Threads (`GetWindowThreadProcessId` → `GetThreadTimes`).

## Einschränkungen

- `WM_COMMAND 0xA21B` ist **undokumentiert**. Ändert Microsoft das Verhalten mit einem Windows-Update,
  fällt `Open()` automatisch auf ein neues Fenster zurück (bzw. wirft bei `fallbackToNewWindow: false`).
- `SetForegroundWindow` unterliegt Windows-Einschränkungen; wird das Programm nicht aus einem Vordergrundprozess
  gestartet, blinkt das Zielfenster ggf. nur in der Taskleiste.
- Die Reihenfolge der Tabs entspricht der Reihenfolge, in der sie geöffnet wurden, nicht zwingend der Anordnung in der Tab-Leiste.
- Als Console App blitzt beim Start (z. B. per Verknüpfung) kurz ein Konsolenfenster auf. Abhilfe:
  `<OutputType>WinExe</OutputType>` in `ExplorerKitSample.csproj` – dann entfallen allerdings Konsolenausgaben.

## Projektstruktur

```
ExplorerKit.slnx
src/
├─ ExplorerKit/                        Klassenbibliothek (Namespace SSP.ExplorerKit)
│  ├─ Explorer.cs                      Öffentlicher Einstiegspunkt: Open, OpenInteractive, GetWindows, CloseWindow
│  ├─ TargetWindow.cs                  Auswahl des Zielfensters
│  ├─ ExplorerWindow.cs                ExplorerWindow, ExplorerTab
│  ├─ OpenResult.cs                    OpenResult, OpenMode
│  ├─ ExplorerWindowNotFoundException.cs
│  ├─ ExplorerFormat.cs                Anzeigeformate für Zeitpunkt und Laufzeit
│  ├─ UI/
│  │  └─ WindowPickerDialog.cs         Auswahldialog (Windows Forms, intern)
│  └─ Internal/
│     ├─ WindowEnumerator.cs           Fenster- und Tab-Ermittlung
│     ├─ TabOpener.cs                  Neuen Tab erzeugen und navigieren
│     ├─ StaThread.cs                  Ausführung auf einem STA-Thread
│     └─ NativeMethods.cs              Win32- und COM-Interop
└─ ExplorerKitSample/                  Console App (Namespace SSP.ExplorerKitSample)
   ├─ Program.cs                       Ablauf, Exit-Codes, Ausgabe von --list
   └─ CommandLine.cs                   Argument-Parsing
```
