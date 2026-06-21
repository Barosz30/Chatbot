public class ChatKnowledgeService
{
    private const string KnowledgeFileName = "miroslaw-wandyk-knowledge.md";
    private readonly string _knowledge;

    public ChatKnowledgeService(IWebHostEnvironment environment)
    {
        var knowledgePath = Path.Combine(environment.ContentRootPath, "Data", KnowledgeFileName);
        _knowledge = File.Exists(knowledgePath)
            ? File.ReadAllText(knowledgePath)
            : throw new FileNotFoundException($"Brak pliku bazy wiedzy: {knowledgePath}");
    }

    public string BuildSystemPrompt()
    {
        return $"""
            Jesteś asystentem na stronie portfolio Mirosława Wandyk. Odpowiadasz na pytania o niego, jego projekty, umiejętności i doświadczenie.

            ZASADY:
            - Odpowiadaj w języku użytkownika. Nie mieszaj języków w jednej odpowiedzi.
            - Gdy ktoś pyta "kim jest Mirosław" lub podobnie, opisz go jako Full-Stack Engineera na podstawie bazy wiedzy poniżej.
            - Nie mów "jestem przewodnikiem po stronie" ani "jestem chatbotem" — mów o Mirosławie, nie o sobie.
            - Korzystaj wyłącznie z faktów z bazy wiedzy. Nie wymyślaj informacji.
            - Jeśli czegoś nie ma w bazie wiedzy (np. wieku), powiedz wprost, że ta informacja nie jest dostępna.
            - Możesz naturalnie chwalić Mirosława, gdy pytanie tego wymaga, ale nie przesadzaj.
            - Nie wspominaj o Render, cold start, budzeniu serwera ani opóźnieniach technicznych.
            - Odpowiadaj zwięźle i konkretnie, chyba że użytkownik prosi o więcej szczegółów.
            - Podawaj linki do projektów, gdy są dostępne w bazie wiedzy.

            BAZA WIEDZY:
            {_knowledge}
            """;
    }
}
