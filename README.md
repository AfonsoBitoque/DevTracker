# DevTracker

Gestor de tarefas de desenvolvimento com integração Git e GitHub. Desktop app multiplataforma em Avalonia UI com .NET 10.

---

## Stack

| Camada | Tecnologia |
|--------|-----------|
| UI | Avalonia UI 12.0.4 (Fluent Theme, Dark) |
| Framework | .NET 10 |
| MVVM | CommunityToolkit.Mvvm 8.4.2 |
| BD | SQLite + Entity Framework Core 10.0.8 |
| DI | Microsoft.Extensions.DependencyInjection |
| Git | CLI nativo (`git`) |
| GitHub API | REST API v3 (HTTPClient) |

---

## Como funciona

### Primeira execução (First Run Setup)

Ao abrir a app pela primeira vez:

1. **Workspace**: escolhes a pasta raiz onde os projetos serao armazenados (ex: `/home/user/workspace`)
2. **Owner**: crias a primeira conta (role `Owner`) — esta e a unica conta que podes criar no setup
3. **Login**: inicias sessao com o utilizador criado

> Se ja existir um utilizador na base de dados, o ecrã de setup e saltado e vais direto para o login.

---

### Dashboard

Main panel with metrics:
- Active tasks by project
- Completed tasks
- Total estimated time
- Projects in progress

---

### Projects

Each project has:
- **Name**, description, color (for visual identification)
- **GitHub URL** (optional, for creating PRs)
- **Workspace folder** (created automatically)
- **State**: `InProgress`, `Paused`, `Cancelled`, `Done`

**Create project**:
- If you enter a GitHub URL, it automatically clones to the workspace
- If you **do not** enter a URL but have a GitHub token configured, it automatically creates a private repository with:
  - Project name as the repo name
  - Project description (if any) as the repo description
- Automatic clone after creation

**Actions**:
- Create / Edit / Delete project
- Clone project (copy to local workspace)
- Change state (with reason if Paused/Cancelled)
- Explore project files

---

### Work Items

Each task belongs to a project and has:
- **Title** and description
- **Type**: Task, Feature, Bug, Improvement, Documentation
- **Priority**: Low, Medium, High, Critical
- **Difficulty**: VeryEasy, Easy, Medium, Hard, VeryHard
- **Estimated time**: <1h, 1-2h, HalfDay, 1Day, 2-3Days, >3Days
- **Status**: Todo, InProgress, Done
- **Comments** and change history

**Kanban Board**: within each project, tasks appear in 3 columns (Todo / Active / Done).

---

### Integrated Git Workflow (Start Task → Finish Task → PR)

DevTracker automates the Git/GitHub workflow for each task:

#### 1. Start Task

- Creates branch: `feature/task-N-task-title`
  - Title is sanitized (spaces → dashes, invalid characters removed)
  - Example: `feature/task-2-Add-header`
- Faz commit de snapshot com mensagem: `Snapshot before starting task: Titulo`
- Muda estado da tarefa para `InProgress`

> Se o git falhar, o estado da tarefa **nao muda** (transacao atomica).

#### 2. Work on the task

Make your code changes within the project folder. DevTracker does not interfere — use your preferred editor/IDE.

#### 3. Finish Task

- Gets the diff of changes via `git diff`
- Shows diff **side-by-side** (two columns side by side):
  - **Left** (red): removed code
  - **Right** (green): added code
  - **Context** (gray): unchanged lines
- Commits on the current branch with message: `Finished task: Title`
- Changes task status to `Done`

> If git fails, the status **does not change**.

#### 4. Create Pull Request

- Faz **push** do branch para o GitHub (com retry automatico se falhar)
- Deteta o **default branch** do repositorio via API (main, master, etc.)
- Creates PR with title: `#N Task Title`
- Returns the PR link

**Automatic fixes**:
- If no upstream branch, pushes with `-u`
- If no commits between branches, creates empty commit to avoid error 422

---

### Diff Viewer (side-by-side)

Side-by-side diff viewer with color coding:

| Color | Meaning |
|-------|---------|
| Dark red background | Removed code |
| Dark green background | Added code |
| Dark gray background | Context (no changes) |
| Darker background | Headers (`diff --git`, `@@`) |

- Monospace font (`JetBrains Mono`)
- No line wrapping (perfect alignment)
- Resizable window with `SizeToContent`

---

### Authentication & Permissions

**Roles**:
- `Owner` — created during initial setup, can do everything
- `Admin` — gerir utilizadores, ver auditoria
- `Maintainer` — criar/editar projetos e tarefas
- `Reader` — apenas leitura

**Seguranca**:
- Sessao com timeout de 30 segundos (bloqueia se inativo)
- Re-autenticacao obrigatoria para operacoes destrutivas (delete, deactivate, mudar estado critico)
- Token do GitHub **nunca** gravado em disco (apenas em memoria)

---

## Estrutura do Projeto

```
src/
├── DevTracker.Core/              # Entidades, enums, value objects
│   └── Entities/
│       ├── Project.cs
│       ├── WorkItem.cs
│       ├── User.cs
│       └── ...
├── DevTracker.Application/       # Interfaces, DTOs, validadores
│   ├── Abstractions/Services/    # IGitService, IGitHubService, etc.
│   ├── DTOs/                     # Requests e responses
│   └── Validators/               # FluentValidation
├── DevTracker.Infrastructure/    # Implementacoes concretas
│   ├── Persistence/              # EF Core, AppDbContext
│   ├── Services/                 # GitService, GitHubService, etc.
│   └── Security/                 # Auth, permissoes
└── DevTracker.Desktop/           # UI Avalonia
    ├── Views/                    # AXAML (XAML da Avalonia)
    ├── ViewModels/               # MVVM com CommunityToolkit
    ├── Converters/               # IValueConverters
    └── Navigation/               # DialogWindow, NavigationService
```

Docs detalhados em [`docs/`](docs/).

---

## Build & Run

### Requirements
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Git instalado no sistema
- (Opcional) GitHub Personal Access Token para PRs

### Build

```bash
# Clonar
git clone https://github.com/AfonsoBitoque/DevTracker.git
cd DevTracker

# Build
dotnet build

# Testes (45 testes)
dotnet test
```

### Run

```bash
cd src/DevTracker.Desktop
dotnet run
```

---

## Publicar como Aplicacao Desktop

### Linux (x64)

```bash
cd src/DevTracker.Desktop

# Publish self-contained
dotnet publish -c Release -r linux-x64 --self-contained \
  -o bin/Release/net10.0/linux-x64/publish

# Instalar localmente
mkdir -p ~/.local/share/devtracker
cp -r bin/Release/net10.0/linux-x64/publish/* ~/.local/share/devtracker/

# Icone SVG (opcional)
cp icon.svg ~/.local/share/devtracker/

# Criar .desktop file
cat > ~/.local/share/applications/devtracker.desktop << 'EOF'
[Desktop Entry]
Name=DevTracker
Comment=Gestor de tarefas de desenvolvimento
Exec=/home/USER/.local/share/devtracker/DevTracker.Desktop
Icon=/home/USER/.local/share/devtracker/icon.svg
Type=Application
Terminal=false
Categories=Development;ProjectManagement;
StartupNotify=true
EOF

# Substituir USER pelo teu username
sed -i "s|/home/USER|/home/$USER|g" ~/.local/share/applications/devtracker.desktop

# Copiar para Desktop
cp ~/.local/share/applications/devtracker.desktop ~/Desktop/
chmod +x ~/Desktop/devtracker.desktop

# Atualizar menu de aplicacoes
update-desktop-database ~/.local/share/applications/
```

### Windows (x64)

```bash
cd src/DevTracker.Desktop

# Publish self-contained para Windows
dotnet publish -c Release -r win-x64 --self-contained \
  -o bin/Release/net10.0/win-x64/publish
```

O output sera uma pasta com:
- `DevTracker.Desktop.exe` — executavel principal
- Todas as DLLs .NET incluidas (self-contained, nao precisa de runtime instalado)
- `icon.svg` — icone da aplicacao

**Para distribuir:**
1. Comprimir a pasta `publish/` num ZIP
2. Enviar ao utilizador
3. O utilizador extrai e corre `DevTracker.Desktop.exe`

**Para criar instalador (opcional):**

Usar [Inno Setup](https://jrsoftware.org/isinfo.php) ou [WiX](https://wixtoolset.org/):

```pascal
; Exemplo Inno Setup script básico
[Setup]
AppName=DevTracker
AppVersion=1.0
DefaultDirName={autopf}\DevTracker
OutputDir=.

[Files]
Source: "bin\Release\net10.0\win-x64\publish\*"; DestDir: "{app}"; Flags: recursesubdirs

[Icons]
Name: "{group}\DevTracker"; Filename: "{app}\DevTracker.Desktop.exe"
Name: "{autodesktop}\DevTracker"; Filename: "{app}\DevTracker.Desktop.exe"
```

### macOS (x64 / Apple Silicon)

```bash
cd src/DevTracker.Desktop

# Intel Mac
dotnet publish -c Release -r osx-x64 --self-contained \
  -o bin/Release/net10.0/osx-x64/publish

# Apple Silicon (M1/M2/M3)
dotnet publish -c Release -r osx-arm64 --self-contained \
  -o bin/Release/net10.0/osx-arm64/publish
```

Para criar `.app` bundle:
```bash
cp -r bin/Release/net10.0/osx-arm64/publish DevTracker.app/Contents/MacOS/
# Criar Info.plist e icon.icns (ver docs da Apple)
```

---

## Dados e Configuracao

| Ficheiro | Localizacao | Descricao |
|----------|-------------|-----------|
| Base de dados | `~/.local/share/DevTracker/data.db` | SQLite (utilizadores, projetos, tarefas) |
| Configuracoes | `~/.local/share/DevTracker/config.json` | Workspace root, tema |
| Logs | `~/.local/share/DevTracker/logs/` | Ficheiros de log |
| DPAPI Keys | `~/.local/share/DevTracker/keys/` | Data protection |

> On Windows: replace `~/.local/share/DevTracker` with `%LOCALAPPDATA%\DevTracker`.

**Note**: No GitHub token is saved to disk. It is requested in memory each session.

---

## License

Apache 2.0
