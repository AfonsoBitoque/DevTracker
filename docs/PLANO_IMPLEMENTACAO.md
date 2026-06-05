# Plano de Implementação: Melhorias DevTracker

Este documento detalha o plano de implementação passo a passo para as atualizações de Prioridade Alta e Média (Tópicos 1, 2 e 3 da Análise do Projeto).

---

## 1. Evolução da Integração Git

A ideia aqui é transformar o DevTracker numa ferramenta que suporte o fluxo de trabalho moderno (Git Flow / GitHub Flow).

### 1.1 Branching Automático
Em vez de fazermos commit na `main`, cada tarefa deve ter o seu próprio branch.

**Passo a passo:**
1. Modificar o `WorkItemDetailViewModel.cs` no método `StartTaskAsync()`.
2. Após o repositório estar inicializado, gerar um nome de branch baseado na tarefa. 
   - *Exemplo:* `feature/task-12-login-page` (usando o Tipo, Número e Título limpo).
3. Adicionar o comando `git checkout -b <nome-do-branch>` no `GitService.cs`.
4. No `FinishTaskAsync()`, o commit e o push já vão usar este novo branch porque o repositório local estará nele.

### 1.2 Restauro do DiffView Avançado (AvaloniaEdit)
O atual `Alert` de 500 caracteres é uma limitação temporária. Precisamos de um visualizador real de código.

**Passo a passo:**
1. Instalar o pacote NuGet `AvaloniaEdit` (`dotnet add package Avalonia.AvaloniaEdit`).
2. Criar uma definição de sintaxe (Highlighting) personalizada para ficheiros `.diff`.
   - Linhas que começam com `+` ficam com fundo verde/texto verde escuro.
   - Linhas que começam com `-` ficam com fundo vermelho/texto vermelho escuro.
3. Atualizar o `DiffView.axaml` para usar o componente `<AvaloniaEdit:TextEditor IsReadOnly="True" />`.
4. Atualizar o `DiffViewModel` para alimentar o `TextEditor` com o texto completo do Diff, sem causar travamentos de UI.

### 1.3 Criação Automática de Pull Requests
Quando a tarefa for terminada e o código for para o GitHub, podemos criar um PR automaticamente.

**Passo a passo:**
1. Instalar a biblioteca `Octokit` (SDK Oficial do GitHub para .NET).
2. No `GitHubService`, criar um método `CreatePullRequestAsync(string repoOwner, string repoName, string branch, string title, string body)`.
3. No `FinishTaskAsync()`, após o push com sucesso:
   - Extrair o `repoOwner` e `repoName` do URL do projeto.
   - Chamar o método do `GitHubService` para abrir um PR do branch atual contra a `main`.

---

## 2. Segurança Reforçada (Armazenamento Seguro)

Atualmente guardamos as credenciais (GitHub Token) num ficheiro `.txt` em plain text. Se o computador for comprometido, o token está exposto.

### 2.1 Implementar Gestor de Segredos Multi-Plataforma
Como o Avalonia funciona em Windows, macOS e Linux, precisamos de guardar o token de forma nativa e segura em cada SO (Windows Credential Manager, macOS Keychain, Linux Secret Service).

**Passo a passo:**
1. Instalar um pacote cross-platform para chaves, como `Xamarin.Essentials.Interfaces` ou gerir com o `Microsoft.AspNetCore.DataProtection` configurado para armazenamento local encriptado. 
   - *Alternativa mais direta:* Utilizar a API do SO via P/Invoke ou pacotes focados em Desktop como `Meziantou.Framework.Win32.CredentialManager` (se for só Windows) ou abstrações que suportem Linux/Mac.
2. Criar a interface `ISecureStorageService`:
   ```csharp
   public interface ISecureStorageService 
   {
       Task SaveSecretAsync(string key, string value);
       Task<string?> GetSecretAsync(string key);
       Task DeleteSecretAsync(string key);
   }
   ```
3. Implementar a lógica deste serviço que encripte a string antes de gravar no disco (ou use a Keyring do SO).
4. Registar como `Singleton` no `App.axaml.cs`.
5. Substituir as chamadas de leitura/escrita de ficheiros no `SettingsViewModel` e `WorkItemDetailViewModel` por injeções do `ISecureStorageService`.

---

## 3. Melhorias UI/UX

Melhorar a percepção de performance da app e facilitar a organização.

### 3.1 Feedback de Long-Running Tasks (Estado "Ocupado")
Quando a aplicação faz push, pode demorar alguns segundos e a UI parece "congelada".

**Passo a passo:**
1. No `ViewModelBase`, adicionar duas propriedades `[ObservableProperty]`:
   - `bool _isBusy;`
   - `string _busyMessage = string.Empty;`
2. No `MainWindow.axaml` ou num componente base, adicionar um Overlay (por exemplo, um `<Grid>` semi-transparente com `<ProgressRing />` e um `<TextBlock Text="{Binding BusyMessage}" />`) cuja visibilidade (`IsVisible`) está ligada ao `IsBusy`.
3. Atualizar os comandos demorados:
   ```csharp
   IsBusy = true;
   BusyMessage = "A enviar código para o GitHub...";
   try {
       await gitService.PushAsync(...);
   } finally {
       IsBusy = false;
   }
   ```

### 3.2 Dashboard Analítico (Ecrã Inicial)
Dar uma visão geral ao utilizador mal este faz login.

**Passo a passo:**
1. Criar `DashboardViewModel.cs` e `DashboardView.axaml`.
2. Adicionar biblioteca gráfica (ex: `LiveChartsCore.SkiaSharpView.Avalonia`).
3. No ViewModel, interrogar a Base de Dados (ex: `IWorkItemService.GetMetricsAsync()`) para retornar estatísticas (Tarefas Todo vs Done, contagem de bugs).
4. No View, mostrar:
   - *Top Cards:* Total Projetos, Total Tarefas Concluídas este mês.
   - *Gráfico de tarte:* Distribuição de tarefas pelo status (Backlog, InProgress, Done).
5. Alterar o `ShellViewModel` para navegar para o `DashboardViewModel` logo após o login, em vez de ir diretamente para a lista de projetos.

### 3.3 Filtros no Kanban
Com projetos maiores, o board Kanban ficará cheio e difícil de ler.

**Passo a passo:**
1. No `ProjectDetailViewModel`, adicionar propriedades como `SearchQuery`, `SelectedAssigneeFilter`, `SelectedTypeFilter`.
2. Adicionar uma barra de ferramentas no `ProjectDetailView.axaml` acima do board Kanban, contendo caixas de texto e dropdowns interligados a estas propriedades.
3. Criar um método `ApplyFilters()` que seja invocado sempre que um dos filtros mude.
4. O método atualizará a coleção principal de `KanbanColumns`, recriando as listas de `KanbanCards` apenas com os items que cumpram as condições (ex: `item.Title.Contains(SearchQuery)`).
