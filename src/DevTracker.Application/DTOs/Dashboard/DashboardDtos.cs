namespace DevTracker.Application.DTOs.Dashboard;

/// <summary>Métricas principais da dashboard do utilizador.</summary>
public sealed record DashboardMetricsDto(
    int TotalProjects,
    int ActiveProjects,
    int CompletedTasks,
    int PendingTasks,
    int InProgressTasks,
    double EstimatedHoursRemaining,
    double AiUsagePercentage,
    TaskBreakdownDto TaskBreakdown);

/// <summary>Breakdown de tasks por categoria.</summary>
public sealed record TaskBreakdownDto(
    DifficultyBreakdownDto Difficulty,
    PriorityBreakdownDto Priority,
    TypeBreakdownDto Type);

/// <summary>Breakdown por dificuldade (tasks por fazer).</summary>
public sealed record DifficultyBreakdownDto(
    int VeryEasy,
    int Easy,
    int Medium,
    int Hard,
    int VeryHard);

/// <summary>Breakdown por prioridade (tasks por fazer).</summary>
public sealed record PriorityBreakdownDto(
    int Low,
    int Medium,
    int High,
    int Critical);

/// <summary>Breakdown por tipo (todas as tasks).</summary>
public sealed record TypeBreakdownDto(
    int Task,
    int Feature,
    int Bug,
    int Improvement,
    int Documentation);
