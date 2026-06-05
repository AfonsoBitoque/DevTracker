# DevTracker — Visual Identity & UI Design Context

> Ficheiro de contexto: identidade visual, direção de design e guidelines de interface.
> Focado em um estilo simples, formal, profissional e sofisticado, evitando o "look padrão de IA".

---

## 1. Princípios de design

O DevTracker é uma ferramenta de trabalho para developers e engenheiros — não uma landing page de marketing.
A identidade visual deve refletir:

- **Simplicidade pragmática**: tudo tem função; nenhuma decoração gratuita.
- **Formal e profissional**: sem ilustrações infantis, mascotes, gradients exagerados ou glassmorphism pesado.
- **Sofisticação discreta**: pequenos detalhes na tipografia, espaçamento, alinhamentos e microinterações.
- **Atemporalidade**: não depender fortemente de tendências de 1 ano (neubrutalismo extremo, glassmorphism full-screen, etc.) para não ficar datado.
- **Foco no conteúdo**: dashboards e listas devem destacar dados e relações, não cromas e efeitos.

Trends 2025–2026 relevantes a adotar com *peso leve*:
- microinterações sutis (hover states claros, feedback de loading, transitions suaves). [web:36][web:39][web:40][web:45]
- tipografia com hierarquia forte e legível. [web:40][web:42]
- minimalismo funcional nos dashboards (menos frames, mais espaço em branco). [web:37][web:43]

Trends a **evitar ou reduzir ao mínimo**:
- gradients super saturados tipo Dribbble/marketing. [web:36][web:48]
- 3D/AR/VR estético sem valor para um app de produtividade. [web:36][web:39][web:45]
- neubrutalismo pesado (blocos ultra coloridos, cantos agressivos) para um produto de uso diário. [web:36][web:48]

---

## 2. Personalidade visual do DevTracker

Palavras-chave:

- **"Engineering-grade"**: parece ferramenta que equipes de engenharia sérias usariam.
- **Disciplinado**: grelha, alinhamento e ritmo de tipografia consistentes.
- **Neutro elegante**: não puxa demasiado para "dark hacker" nem para "white enterprise boring".
- **Offline-first**: visual inspirado em IDEs e ferramentas de versionamento mais do que em websites.

Exemplos de referência (não para copiar, só como direção mental):

- Interfaces de tools como Linear, JetBrains IDEs, VS Code com tema profissional, dashboards B2B minimalistas de 2025–2026. [web:37][web:40][web:43][web:46][web:49]

---

## 3. Paleta de cores

### 3.1 Princípios

- Base neutra em tons de **cinza frio**.
- 1 cor primária para ações importantes (botões principais, links) — azul petróleo / teal escuro.
- 1 cor secundária de acento suave (para estados secundários, bordas selecionadas) — talvez um roxo/grisáceo discreto.
- Sistema de estados (success / warning / error / info) alinhado com padrões enterprise.
- Suporte a **modo claro e escuro**, mantendo contraste AA/AAA.

### 3.2 Paleta inicial (conceitual)

> Nota: valores HEX são placeholders; podes ajustar depois no Figma.

- **Neutros (UI base)**
  - `--color-bg-light`: `#F5F6F8`
  - `--color-bg-elevated`: `#FFFFFF`
  - `--color-bg-dark`: `#111318`
  - `--color-surface-dark`: `#181B22`
  - `--color-border-subtle`: `#D0D3DD`
  - `--color-border-strong`: `#A3A7B3`
  - `--color-text-primary-light`: `#151924`
  - `--color-text-secondary-light`: `#4A4F5D`
  - `--color-text-primary-dark`: `#F5F6F9`
  - `--color-text-secondary-dark`: `#AEB3C2`

- **Primária**
  - `--color-primary`: `#2563EB` (azul profundo, estilo tooling moderno)
  - `--color-primary-dark`: `#1D4ED8`
  - `--color-primary-soft`: `#E0ECFF`

- **Acento secundário (opcional)**
  - `--color-accent`: `#6366F1` (indigo suave)
  - `--color-accent-soft`: `#E5E7FF`

- **Estados**
  - Success: `#16A34A` / background `#DCFCE7`
  - Warning: `#D97706` / background `#FEF3C7`
  - Error: `#DC2626` / background `#FEE2E2`
  - Info: `#0284C7` / background `#E0F2FE`

### 3.3 Regras de uso

- Vista principal (listas, boards) usa **fundo neutro** e **superfícies claras/escuro suave**, sem gradients.
- A cor primária aparece em:
  - Botão primário
  - Links e ações principais
  - Realce discreto em seleções (borda/linha).
- A cor secundária é usada em:
  - Tags/labels específicas
  - Indicadores de seleção ativos em navegação lateral.

---

## 4. Tipografia

### 4.1 Objetivos

- Legibilidade máxima em sessões longas de uso (4–8h/dia).
- Hierarquia clara entre títulos, subtítulos, conteúdo, meta-informação.
- Aparência moderna, mas não "futurista".

### 4.2 Stack sugerida

- Fonte UI principal: **Inter** ou **SF Pro / system UI** — ambas amplamente usadas em produtos modernos por equilíbrio entre neutralidade e legibilidade. [web:42][web:37]
- Fallback: `system-ui, -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif`.
- Para monoespaçada (logs, paths, git commands): `JetBrains Mono`, `Fira Code` ou `Cascadia Code`.

### 4.3 Escala tipográfica (base conceitual)

- H1 (título de página): 24–28 px, sem bold extremo (peso 600).
- H2 (secções dentro da página): 18–20 px, peso 600.
- H3 (subtítulos de card/lista): 16 px, peso 500.
- Body: 14 px, peso 400.
- Meta (timestamps, labels pequenas): 12 px, peso 400, cor secundária.

### 4.4 Regras

- No máximo **2 famílias** (UI + mono).
- Nunca usar itálico decorativo em massa; só em casos pontuais.
- Títulos e labels de coluna em maiúsculas leves (tracking aumentado) opcionalmente, se não prejudicar legibilidade.

---

## 5. Layout e espaçamento

### 5.1 Grelha / ritmo

- Grid base: increments de **4 px**, com espaçamentos mais comuns de 8/12/16/24 px.
- Margens exteriores em ecrãs desktop: 24–32 px.
- Cards/list rows: altura mínima confortável (48–56 px) para reduzir densidade visual extrema.

### 5.2 Estrutura de páginas típicas

- Shell com sidebar esquerda de largura fixa (ex: 240 px), conteúdo scrollável à direita.
- Header de página com:
  - título
  - breve descrição opcional
  - ações principais (botão primário + ações secundárias discretas).
- Conteúdo organizado em
  - colunas/responsivo para dashboards
  - tabelas/listas para tarefas e projetos.

### 5.3 Densidade controlada

- Evitar UI ultra densa ao estilo "excel cheio".
- Preferir compressão de informação com filtros, tabs e colunas colapsáveis.

---

## 6. Componentes fundamentais (look & feel)

### 6.1 Botões

- **Primary button**
  - Fundo: `--color-primary`
  - Texto: branco
  - Raio: 6–8 px (sem bordas super arredondadas tipo "pill" genérico de IA)
  - Hover: escurecer ligeiramente + sombra muito suave
  - Disabled: opacidade reduzida + sem sombra

- **Secondary button**
  - Fundo: `--color-bg-elevated`
  - Borda: `--color-border-subtle`
  - Texto: `--color-text-primary`
  - Hover: leve mudança no background (`--color-primary-soft` muito claro)

- **Ghost / text button**
  - Sem preenchimento, apenas texto e possível ícone
  - Uso para ações de menor prioridade ("Ver audit log", "Ver mais").

### 6.2 Cards e painéis

- Superfícies com fundo sólido neutro (sem gradient), borda subtil e sombra mínima ou nenhuma.
- Cantos: 8 px — consistentes em toda a UI.
- Título em H3, meta-informação em linha abaixo.

### 6.3 Tabelas e listas

- Linhas com zebra suave opcional (tons muito leves).
- Separadores horizontais finos (`1px`, `--color-border-subtle`).
- Hover na linha: fundo ligeiramente destacado, sem underline.
- Ações de linha (editar, apagar, abrir) aparecem alinhadas à direita, visíveis só em hover para reduzir ruído.

### 6.4 Kanban / board de tarefas

- Colunas com título em H3 e contador de itens.
- Cartões com fundo sólido, sombras suaves, arrasto visual discreto (leve scaling + sombra ao arrastar).
- Bagunça visual mínima: evitar tags com demasiadas cores.

---

## 7. Estilo de ícones e ilustrações

### 7.1 Ícones

- Estilo linear (outline) consistente, stroke uniforme.
- Tamanho base: 16 px e 20 px.
- Usar packs coerentes (ex.: Phosphor, Lucide, ou um set custom prosseguindo mesmo estilo).
- Ícones de cor única (texto primário ou secundário), sem gradients.

### 7.2 Ilustrações

- **Evitar** ilustrações genéricas tipo "startup people", 3D blobs, gradients neon.
- Se forem usadas empty states, preferir desenhos minimalistas monocromáticos ou duotone muito neutros.

---

## 8. Modo escuro vs. claro

### 8.1 Abordagem

- O **modo escuro** é o padrão recomendado para um app de produtividade de developers, com modo claro como opção.
- Troca entre temas deve:
  - respeitar contraste mínimo WCAG AA
  - não inverter semanticamente cores de estado.

### 8.2 Regras

- No dark mode, fundos aproximam-se de `#111318`–`#181B22`, textos mantêm contraste alto. [web:36][web:37][web:51]
- Tabelas: linhas continuam claramente distinguíveis, mas sem borders muito agressivas.
- Nunca usar texto cinza claro sobre cinza médio (problemas de legibilidade comuns em temas "bonitos" mas pouco usáveis).

---

## 9. Microinterações e motion

### 9.1 Princípios

- Motion serve para **clareza de contexto**, não para exibicionismo. [web:36][web:40][web:45]
- Duração padrão: 120–180 ms.
- Curvas de animação suaves (ease-out / ease-in-out), sem bounce.

### 9.2 Exemplos

- Hover em botões: transição suave de cor de fundo/borda.
- Abrir detalhes de tarefa: card expande com animação vertical leve.
- Mudança de estado no board: o cartão move-se com pequena transição de posição.
- Feedback de sucesso: pequeno highlight do card/linha durante 600 ms.

Evitar: skeletons e loaders over-designed; preferir spinners discretos ou barras de progresso finas.

---

## 10. Identidade de marca (logotipo e nome)

### 10.1 Nome

- `DevTracker` (ou o nome final que escolheres) deve ser tratado sempre de forma consistente em UI, docs e config. [web:38][web:44][web:50]

### 10.2 Logotipo (conceito)

- Logotipo baseado em **wordmark tipográfico** + símbolo simples inspirado em
  - um commit graph minimal
  - um nó de grafo / estado de projeto
  - uma pasta/projeto estilizada com checkmarks.
- Sem gradients complexos; preferir versões:
  - full color (primária + neutros)
  - mono clara (para fundos escuros)
  - mono escura (para fundos claros).

### 10.3 Aplicação

- App desktop: logotipo simplificado (ícone) na janela e splash.
- Sidebar: apenas wordmark compacto ou ícone + nome.
- Documentação: uso consistente do wordmark e cores primárias.

---

## 11. Anti-padrões de "UI gerada por IA" a evitar

Para prevenir que a UI pareça mais "template generativo" do que ferramenta cuidadosamente desenhada:

1. **Excesso de glassmorphism / blur**
   - Usar superfícies sólidas; somente componentes muito específicos podem ter leve transparência.

2. **Gradients arco-íris em todo o lado**
   - Se forem usados gradients, limitar a 1–2 componentes hero específicos, nunca em toda a app.

3. **Cantinhos ultra redondos em tudo (pill mania)**
   - Manter raios entre 6–10 px; não transformar tudo em pílula.

4. **Shadow pesadão tipo "cartão a flutuar"**
   - Sombra quase imperceptível ou nenhuma; reservar sombra um pouco mais forte para modais.

5. **Ícones de packs misturados**
   - Escolher um sistema de ícones único e manter consistência de stroke, ângulo, raio.

6. **Fontes genéricas sem hierarquia**
   - Mesmo usando Inter/system UI, a hierarquia e spacing cuidadosos já diferenciam do gerado por IA.

7. **Overload de chips, badges e tags coloridos**
   - Limitar número de cores usadas simultaneamente no ecrã.

---

## 12. Entregáveis recomendados (fora do código)

Para consolidar a identidade visual, idealmente o projeto teria também:

- Ficheiro Figma/UX com:
  - Library de cores (tokens)
  - Typestyles (H1, H2, Body, Meta, Code)
  - Componentes base (botões, inputs, tabelas, cards, tags, board columns)
- Mini brand sheet em PDF com:
  - logotipo
  - paleta de cor
  - tipografia
  - 1–2 mockups principais (dashboard e board de tarefas).

Estes artefactos não são gerados automaticamente aqui, mas este DESIGN.md serve como contrato textual para guiar o trabalho no Figma e na implementação em XAML.

---

## 13. Regras finais de identidade visual

1. O conteúdo (projetos, tarefas, estados) manda; UI serve, não se exibe.
2. Paleta neutra com acentos bem controlados.
3. Tipografia simples, coerente e hierárquica.
4. Layouts baseados em grelha e espaço em branco generoso.
5. Motion discreto, sempre com propósito.
6. Nada de tendências extremas que comprometam uso diário ou tornem a interface "moda de um ano".

