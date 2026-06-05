# DevTracker - Análise do Projeto e Roadmap

## 📌 O que é o DevTracker atualmente?

O **DevTracker** é uma aplicação desktop de gestão de projetos e controlo de versões, construída para ajudar programadores a gerir as suas tarefas e código de forma integrada. Desenvolvido com **C# (.NET)** e **Avalonia UI** (framework cross-platform), utiliza o padrão **MVVM** (Model-View-ViewModel) e uma arquitetura limpa (Clean Architecture).

### 🚀 Funcionalidades Principais Implementadas:
1. **Gestão de Autenticação e Autorização:**
   - Sistema de Login e Setup inicial (First Run).
   - Controlo de acessos baseado em perfis (Owner, Admin, Maintainer, Reader).
   - Bloqueio de sessão automático por inatividade.

2. **Gestão de Projetos:**
   - Criação e configuração de projetos com associação a diretórios locais (Workspace).
   - Associação de repositórios GitHub aos projetos.
   - Controlo de estado do projeto (Em progresso, Pausado, Concluído, etc.).

3. **Kanban e Work Items (Tarefas):**
   - Gestão de tarefas, bugs e melhorias.
   - Ciclo de vida completo das tarefas (Todo, InProgress, Done, etc.).

4. **Integração Git e GitHub:**
   - **Fluxo "Começar Tarefa":** Inicializa o repositório local e faz um snapshot automático do código atual.
   - **Fluxo "Terminar Tarefa":** Mostra o diff (diferenças) do código alterado, faz o commit automático localmente e envia (push) para o GitHub usando HTTPS e Token Pessoal.

5. **Auditoria e Segurança:**
   - Registo detalhado de ações dos utilizadores (Audit Log).
   - Soft-delete para não perder dados acidentalmente.
   - Ficheiro local de configurações para armazenar credenciais (GitHub Token).

### 🛠 Stack Tecnológico:
- **UI:** Avalonia UI (XAML)
- **Design Pattern:** MVVM (CommunityToolkit.Mvvm)
- **Base de Dados:** SQLite + Entity Framework Core
- **Interações de Sistema:** CLI (Git via chamadas de terminal)

---

## 🔮 Sugestões de Atualizações Futuras (Roadmap)

Embora o projeto tenha uma base sólida, existem várias áreas de melhoria para o tornar numa ferramenta verdadeiramente profissional.

### 1. Evolução da Integração Git (Prioridade Alta)
- **Branching Automático:** Em vez de fazer push sempre para o branch atual/`main`, a ação "Começar Tarefa" deveria criar um branch específico (ex: `feature/task-123`).
- **Restaurar DiffView Avançado:** O visualizador de diferenças foi simplificado num `Alert` para evitar um crash de memória. O ideal é usar uma biblioteca como `AvaloniaEdit` para mostrar o código lado a lado, com syntax highlighting e linhas coloridas (verde/vermelho).
- **Pull Requests:** Adicionar um botão que interage com a API do GitHub para abrir um Pull Request automaticamente quando a tarefa for para a coluna "Review".
- **Clonar Repositórios:** Permitir fazer `git clone` a partir de um URL logo na criação do projeto, em vez de apenas fazer `git init` em pastas existentes.

### 2. Segurança Reforçada (Prioridade Alta)
- **Armazenamento Seguro de Credenciais:** Atualmente, o GitHub Token e Username são guardados num ficheiro `settings.txt` em texto limpo. Deveria ser usado o mecanismo nativo do sistema operativo para encriptação (ex: *Data Protection API* no Windows, ou equivalentes seguros em Linux/macOS).

### 3. Melhorias UI/UX (Prioridade Média)
- **Visualização Analítica:** Adicionar um Dashboard no ecrã inicial com estatísticas (tarefas concluídas, tempo gasto, repositórios mais ativos).
- **Filtragem e Pesquisa:** Pesquisa avançada e filtros no Kanban board (por Assignee, Label, Prioridade).
- **Feedback de Long-Running Tasks:** Implementar *Loading Spinners* na UI enquanto operações demoradas (como o `git push`) estão a decorrer em background, para que o software não pareça "congelado".

### 4. Funcionalidades de Equipa e Nuvem (Prioridade Média-Baixa)
- **Sincronização Multi-Dispositivo:** Atualmente usa SQLite local. Uma transição para um servidor de base de dados (PostgreSQL/SQL Server) permitiria que uma equipa inteira visse os mesmos projetos e boards Kanban ao mesmo tempo.
- **Notificações:** Alertas do sistema (System Tray) para quando uma tarefa for atribuída ou houver erros de sincronização em background.

### 5. Qualidade de Código e Manutenção
- **Cobertura de Testes (Unit Testing):** Adicionar um projeto xUnit/NUnit para testar a lógica de negócio principal (serviços e viewmodels), prevenindo novos crashes (segfaults) em cenários inesperados.
- **Gestão de Exceções Git:** O parser do terminal do Git pode ser frágil dependendo da língua do SO ou versão do Git. Usar a biblioteca nativa **`libgit2sharp`** no futuro seria uma opção muito mais robusta do que usar o `ProcessStartInfo` executando CLI.
