using System.Net.Http.Json;
using System.Text.Json;
using ApplicationLayer.Interfaces;
using CoreModels.DTOs;
using DataAccessLayer.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BusinessLayer.Services;

public sealed class GeminiSchemaAnalysisService(
    IngestionDbContext database,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<GeminiSchemaAnalysisService> logger,
    IPendingUploadStore pendingUploads) : IAiAnalysisService
{
    private DateTime Now => DateTime.UtcNow.AddHours(configuration.GetValue<double?>("Application:Time:IndiaStandardTimeOffsetHours") ?? 5.5);
    public async Task<AiSchemaAnalysisDto?> AnalyzeSchemaAsync(string userId, long uploadId, Token token)
    {
        var upload = await database.Uploads
            .AsNoTracking()
            .Include(item => item.Sheets)
            .ThenInclude(sheet => sheet.Columns)
            .SingleOrDefaultAsync(item => item.UploadId == uploadId && (userId == "*" || item.User != null && item.User.UserName == userId), token);

        if (upload is null) return null;

        var previousUpload = await database.Uploads
            .AsNoTracking()
            .Include(item => item.Sheets)
            .ThenInclude(sheet => sheet.Columns)
            .Where(item => item.UserId == upload.UserId && item.UploadId != upload.UploadId && item.UploadedOn < upload.UploadedOn)
            .OrderByDescending(item => item.UploadedOn)
            .FirstOrDefaultAsync(token);

        var apiKey = configuration["Gemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey)) apiKey = configuration["GeminiApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey)) apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey)) throw new InvalidOperationException("Gemini API key is not configured on the backend.");

        var model = configuration["Gemini:Model"] ?? "gemini-3.6-flash";
        var schema = upload.Sheets
            .OrderBy(sheet => sheet.SheetOrder)
            .Select(sheet => new
            {
                Sheet = sheet.SheetName,
                Columns = sheet.Columns.OrderBy(column => column.ColumnOrder).Select(column => $"Column{column.ColumnOrder + 1}").ToArray()
            })
            .ToArray();

        var previousSchema = previousUpload?.Sheets
            .OrderBy(sheet => sheet.SheetOrder)
            .Select(sheet => new
            {
                Sheet = sheet.SheetName,
                Columns = sheet.Columns.OrderBy(column => column.ColumnOrder).Select(column => $"Column{column.ColumnOrder + 1}").ToArray()
            })
            .ToArray();

        var prompt = "You are a friendly business data analyst. Produce a clear, client-ready analysis of the entire workbook in simple, conversational language. " +
                     "Analyze every sheet together rather than focusing on one sheet. For every column, explain its likely business meaning, role, domain, naming quality, and relationship to other columns. " +
                     "Explain relationships as useful business prose: state which column likely identifies an entity, which columns describe it, which columns connect records across sheets, and how those relationships support the business process. " +
                     "For every relationship, explain the reasoning from the column names and clearly label uncertain relationships as likely or inferred. Identify how the sheets fit together, repeated fields, likely keys, business processes, structural changes from the previous workbook when available, and a practical target database design. " +
                     "Use everyday business language that a client or non-technical manager can understand. Keep sentences reasonably short, explain technical terms when they are unavoidable, and avoid jargon such as primary key, foreign key, normalization, or schema unless you explain it in simple words. Make every conclusion explainable: say what you observed from the column names, what the relationship probably means, why it matters to the business, and what action you recommend. Do not invent values or statistics. " +
                     "Return a report with exactly these headings:\n" +
                     "1. Executive summary\n" +
                     "2. Workbook and sheet purposes\n" +
                     "3. Column-by-column business interpretation\n" +
                     "4. Relationships and workbook structure\n" +
                     "5. Schema changes from the previous upload\n" +
                     "6. Data model and ingestion recommendations\n" +
                     "7. Business and governance recommendations\n" +
                     "8. Action plan\n" +
                     "After these workbook-level sections, include one clearly separated section for every sheet using the exact heading format `SHEET ANALYSIS: <exact sheet name>`. For each sheet, explain its purpose, then cover every column with these simple labels: `What it means`, `How it connects`, and `Why it matters`. Describe likely relationships and business use with a short example in plain language when helpful, and give practical recommendations. Do not omit any sheet or column.\n" +
                     "Do not output raw SQL, database DDL, JSON, code blocks, backticks, pipe-delimited tables, or fragments such as `table (column, column)`. Do not merely list table names and column names. Every listed column or relationship must be followed by a friendly plain-language explanation of what it represents and how it connects to other fields. Prefer wording such as `This column identifies the use case` and `This field connects the record to the related actor` instead of technical database notation. " +
                     "Do not mention prompts, AI, metadata restrictions, unavailable values, privacy limitations, or analysis limitations in the report. " +
                     "Use the complete workbook schema and do not omit any sheet or column.\n\nWorkbook schema:\n" +
                     JsonSerializer.Serialize(new { CurrentWorkbook = schema, PreviousWorkbook = previousSchema });

        var request = new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } },
            generationConfig = new { temperature = 0.2, maxOutputTokens = 4000 }
        };

        using var http = httpClientFactory.CreateClient("Gemini");
        using var message = new HttpRequestMessage(HttpMethod.Post, $"v1beta/models/{Uri.EscapeDataString(model)}:generateContent");
        message.Headers.Add("x-goog-api-key", apiKey);
        message.Content = JsonContent.Create(request);
        using var response = await http.SendAsync(message, token);
        var payload = await response.Content.ReadAsStringAsync(token);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Gemini schema analysis failed with status {StatusCode}: {Response}", response.StatusCode, payload);
            throw new InvalidOperationException("Gemini could not analyze this schema. Check the Gemini API key and model configuration.");
        }

        using var json = JsonDocument.Parse(payload);
        var analysis = json.RootElement.GetProperty("candidates")[0]
            .GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
        if (string.IsNullOrWhiteSpace(analysis)) throw new InvalidOperationException("Gemini returned an empty schema analysis.");

        return new AiSchemaAnalysisDto(upload.UploadId, upload.OriginalFileName, model, true, schema.Length, schema.Sum(item => item.Columns.Length), analysis, Now);
    }

    public async Task<AiChatResponseDto?> AskAsync(string userId, long uploadId, string question, Token token)
    {
        var upload = await database.Uploads
            .AsNoTracking()
            .Include(item => item.Sheets)
            .ThenInclude(sheet => sheet.Columns)
            .SingleOrDefaultAsync(item => item.UploadId == uploadId && (userId == "*" || item.User != null && item.User.UserName == userId), token);
        if (upload is null) return null;

        var apiKey = configuration["Gemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey)) apiKey = configuration["GeminiApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey)) apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey)) throw new InvalidOperationException("Gemini API key is not configured on the backend.");
        var model = configuration["Gemini:Model"] ?? "gemini-3.6-flash";
        var schema = upload.Sheets.OrderBy(sheet => sheet.SheetOrder).Select(sheet => new
        {
            Sheet = sheet.SheetName,
            Columns = sheet.Columns.OrderBy(column => column.ColumnOrder).Select(column => $"Column{column.ColumnOrder + 1}").ToArray()
        }).ToArray();

        var prompt = "You are a friendly business data assistant. Answer the client's question using only the workbook structure below: sheet names and column names. Never use or request row values, cell contents, uploaded files, or confidential customer data. " +
                     "Explain the answer in casual, clear language for a non-technical client. If the answer is inferred from names, say that it is a likely interpretation. Explain what each relevant column means, how columns or sheets may connect, why the connection matters, and what the client should do next. " +
                     "Do not output SQL, JSON, code, backticks, database DDL, or raw lists without explanations. If the question requires actual row values or file contents, politely explain that this assistant only uses workbook structure and offer a metadata-based alternative. " +
                     $"Client question: {question}\n\nWorkbook structure:\n{JsonSerializer.Serialize(schema)}";
        var request = new { contents = new[] { new { parts = new[] { new { text = prompt } } } }, generationConfig = new { temperature = 0.2, maxOutputTokens = 1200 } };
        using var http = httpClientFactory.CreateClient("Gemini");
        using var message = new HttpRequestMessage(HttpMethod.Post, $"v1beta/models/{Uri.EscapeDataString(model)}:generateContent");
        message.Headers.Add("x-goog-api-key", apiKey);
        message.Content = JsonContent.Create(request);
        using var response = await http.SendAsync(message, token);
        var payload = await response.Content.ReadAsStringAsync(token);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Gemini workbook chat failed with status {StatusCode}: {Response}", response.StatusCode, payload);
            throw new InvalidOperationException("Gemini could not answer this question. Check the Gemini API key and model configuration.");
        }
        using var json = JsonDocument.Parse(payload);
        var answer = json.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
        if (string.IsNullOrWhiteSpace(answer)) throw new InvalidOperationException("Gemini returned an empty answer.");
        return new AiChatResponseDto(upload.UploadId, model, true, answer, Now);
    }

    public async Task<AiHelpResponseDto> AskHelpAsync(string question, Token token)
    {
        var apiKey = configuration["Gemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey)) apiKey = configuration["GeminiApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey)) apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey)) throw new InvalidOperationException("Gemini API key is not configured on the backend.");
        var model = configuration["Gemini:Model"] ?? "gemini-flash-lite-latest";
        var prompt = "You are the friendly in-application help assistant for an enterprise data ingestion platform. Answer the user's question in simple, casual language. Explain the steps for uploading supported files, handling duplicate names, reviewing validation results, proceeding with import, viewing upload history, downloading files, opening Data Explorer, reading charts, switching sheets, using the Workbook Assistant, and contacting support. Do not access or ask for customer file data, row values, credentials, API keys, or passwords. If the question is unrelated to application usage, say what this help assistant can support and suggest a relevant topic. Keep the answer practical and easy to follow.\n\nUser question: " + question;
        var request = new { contents = new[] { new { parts = new[] { new { text = prompt } } } }, generationConfig = new { temperature = 0.25, maxOutputTokens = 900 } };
        using var http = httpClientFactory.CreateClient("Gemini");
        using var message = new HttpRequestMessage(HttpMethod.Post, $"v1beta/models/{Uri.EscapeDataString(model)}:generateContent");
        message.Headers.Add("x-goog-api-key", apiKey);
        message.Content = JsonContent.Create(request);
        using var response = await http.SendAsync(message, token);
        var payload = await response.Content.ReadAsStringAsync(token);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Gemini application help failed with status {StatusCode}: {Response}", response.StatusCode, payload);
            throw new InvalidOperationException("The help assistant is temporarily unavailable. Please try again shortly.");
        }
        using var json = JsonDocument.Parse(payload);
        var answer = json.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
        if (string.IsNullOrWhiteSpace(answer)) throw new InvalidOperationException("The help assistant returned an empty answer.");
        return new AiHelpResponseDto(model, answer, Now);
    }

    public async Task<ColumnMappingResultDto?> MapPendingColumnsAsync(string userId, Guid validationId, Token token)
    {
        if (!pendingUploads.TryGet(validationId, out var pending) || pending is null || !string.Equals(pending.UserId, userId, StringComparison.Ordinal)) return null;
        var sources = pending.Workbook.Sheets.OrderBy(sheet => sheet.Order).SelectMany(sheet => sheet.Columns.OrderBy(column => column.Order).Select(column => new SourceColumnDto(sheet.Name, sheet.Order, column.Name, column.Order, "string"))).ToList();
        var targets = new[] { "Facility", "Manufacture", "Product", "Product Description", "Customer UOM", "Customer Qty" };
        var apiKey = configuration["Gemini:ApiKey"] ?? configuration["GeminiApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey)) return BuildFallbackMapping(validationId, sources, targets, "Gemini is unavailable. Automatic header recommendations are shown instead.");
        var model = configuration["Gemini:Model"] ?? "gemini-3.6-flash";
        var prompt = "You are a precise enterprise data-column mapping assistant. Return JSON only with a mappings array. Analyze every sheet independently and maximize safe semantic matches. The six canonical database target fields are exactly: Facility, Manufacture, Product, Product Description, Customer UOM, Customer Qty. Infer the business meaning of each source header, including abbreviations, acronyms, compound phrases, reordered words, singular/plural forms, regional terminology, and domain-specific wording. Do not require an exact spelling or a fixed synonym list. For example, a field describing a physical operating site can map to Facility, a field describing the maker or supplying organization can map to Manufacture, a field identifying an item can map to Product, a field explaining an item can map to Product Description, a field describing a measurement unit can map to Customer UOM, and a field expressing requested or consumed amount can map to Customer Qty. Use each source column at most once per sheet and map each target at most once per sheet. Never invent a source column, never use row values, and never map a weak or ambiguous relationship. Return one item for each target on each sheet; use targetColumn null when no safe match exists. Confidence rules: 95-100 exact or near-exact, 85-94 strong semantic match, 70-84 plausible semantic match needing review, below 70 must be null and left for manual selection. The confidenceScore must reflect the actual header relationship, and reason must explain it in plain language. Each item must contain sheetName, sheetOrder, sourceColumn, sourceColumnOrder, targetColumn, confidenceScore, confidenceLevel, reason, mappingMethod=AI, isRecommended. Workbook: " + pending.OriginalFileName + ". Source columns: " + JsonSerializer.Serialize(sources);
        var request = new { contents = new[] { new { parts = new[] { new { text = prompt } } } }, generationConfig = new { temperature = 0.1, responseMimeType = "application/json", maxOutputTokens = 2400 } };
        try
        {
            using var http = httpClientFactory.CreateClient("Gemini");
            using var message = new HttpRequestMessage(HttpMethod.Post, $"v1beta/models/{Uri.EscapeDataString(model)}:generateContent");
            message.Headers.Add("x-goog-api-key", apiKey); message.Content = JsonContent.Create(request);
            using var response = await http.SendAsync(message, token); var payload = await response.Content.ReadAsStringAsync(token);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Gemini mapping request failed.");
            using var outer = JsonDocument.Parse(payload); var text = outer.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
            var cleanText = (text ?? "{}").Trim().Replace("```json", string.Empty, StringComparison.OrdinalIgnoreCase).Replace("```", string.Empty).Trim();
            using var json = JsonDocument.Parse(cleanText); var mappings = new List<ColumnMappingDto>();
            if (json.RootElement.TryGetProperty("mappings", out var items) && items.ValueKind == JsonValueKind.Array)
                foreach (var item in items.EnumerateArray())
                {
                    var sheet = item.TryGetProperty("sheetName", out var sheetValue) ? sheetValue.GetString() ?? string.Empty : string.Empty;
                    var source = GetString(item, "sourceColumn", "sourceColumnName");
                    var order = GetInt(item, "sourceColumnOrder", "columnOrder");
                    var target = item.TryGetProperty("targetColumn", out var targetValue) && targetValue.ValueKind != JsonValueKind.Null ? targetValue.GetString()?.Trim() : null;
                    var score = item.TryGetProperty("confidenceScore", out var scoreValue) && scoreValue.TryGetInt32(out var parsedScore) ? Math.Clamp(parsedScore, 0, 100) : 0;
                    if (score < 70) target = null;
                    var sourceInfo = sources.FirstOrDefault(column => string.Equals(column.SheetName.Trim(), sheet.Trim(), StringComparison.OrdinalIgnoreCase) && (column.ColumnOrder == order || order < 0) && string.Equals(column.SourceColumnName.Trim(), source.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (sourceInfo is null || (target is not null && !targets.Contains(target, StringComparer.OrdinalIgnoreCase))) continue;
                    mappings.Add(new ColumnMappingDto(sourceInfo.SheetName, sourceInfo.SheetOrder, sourceInfo.SourceColumnName, sourceInfo.ColumnOrder, target, score, score >= 90 ? "High" : score >= 70 ? "Medium" : "Low", item.TryGetProperty("reason", out var reason) ? reason.GetString() : null, "AI", target is not null && score >= 70));
                }
            logger.LogInformation("AI mapping completed for validation {ValidationId}: {Count} suggestions", validationId, mappings.Count);
            if (mappings.Count == 0) return BuildFallbackMapping(validationId, sources, targets, "Gemini did not return usable matches. Automatic header recommendations are shown instead.");
            var fallback = BuildFallbackMapping(validationId, sources, targets, string.Empty).Mappings;
            foreach (var candidate in fallback)
            {
                var targetAlreadyMapped = mappings.Any(item => item.SheetOrder == candidate.SheetOrder && string.Equals(item.TargetColumn, candidate.TargetColumn, StringComparison.OrdinalIgnoreCase));
                var sourceAlreadyUsed = mappings.Any(item => item.SheetOrder == candidate.SheetOrder && item.ColumnOrder == candidate.ColumnOrder);
                if (!targetAlreadyMapped && !sourceAlreadyUsed) mappings.Add(candidate);
            }
            return new ColumnMappingResultDto(validationId, sources, mappings, targets, true, null);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException)
        {
            logger.LogWarning(exception, "AI mapping failed for validation {ValidationId}; manual mapping remains available", validationId);
            return BuildFallbackMapping(validationId, sources, targets, "Gemini mapping was unavailable. Automatic header recommendations are shown instead.");
        }
    }

    private static string GetString(JsonElement item, params string[] names)
    {
        foreach (var name in names) if (item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String) return value.GetString() ?? string.Empty;
        return string.Empty;
    }

    private static int GetInt(JsonElement item, params string[] names)
    {
        foreach (var name in names) if (item.TryGetProperty(name, out var value) && value.TryGetInt32(out var number)) return number;
        return -1;
    }

    private static ColumnMappingResultDto BuildFallbackMapping(Guid validationId, IReadOnlyList<SourceColumnDto> sources, IReadOnlyList<string> targets, string message)
    {
        var aliases = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Facility"] = ["facility", "site", "site name", "plant", "plant location", "operating site", "factory", "warehouse", "location"],
            ["Manufacture"] = ["manufacture", "manufacturer", "vendor manufacturer", "producer", "producer name", "brand manufacturer", "maker", "maker name", "supplier"],
            ["Product"] = ["product", "product id", "product code", "article number", "sku", "sku number", "material code", "item code"],
            ["Product Description"] = ["product description", "description", "item description", "article details", "sku description", "material details", "product details"],
            ["Customer UOM"] = ["customer uom", "uom", "measure unit", "unit type", "customer unit", "quantity unit", "unit of measure"],
            ["Customer Qty"] = ["customer qty", "customer quantity", "quantity", "qty", "purchase qty", "demand quantity", "customer volume", "required amount"]
        };
        var mappings = new List<ColumnMappingDto>();
        foreach (var sheet in sources.GroupBy(source => source.SheetOrder).OrderBy(group => group.Key))
        {
            var used = new HashSet<int>();
            foreach (var target in targets)
            {
                var best = sheet.Select(source => (source, score: ScoreHeader(source.SourceColumnName, target, aliases[target])))
                    .Where(item => item.score >= 70 && !used.Contains(item.source.ColumnOrder)).OrderByDescending(item => item.score).FirstOrDefault();
                if (best.source is null) continue;
                used.Add(best.source.ColumnOrder);
                mappings.Add(new ColumnMappingDto(best.source.SheetName, best.source.SheetOrder, best.source.SourceColumnName, best.source.ColumnOrder, target, best.score, best.score >= 90 ? "High" : best.score >= 70 ? "Medium" : "Low", $"The source header '{best.source.SourceColumnName}' is a common name for {target}.", "Automatic", true));
            }
        }
        return new ColumnMappingResultDto(validationId, sources, mappings, targets, false, message);
    }

    private static int ScoreHeader(string header, string target, IReadOnlyList<string> aliases)
    {
        var value = NormalizeHeader(header);
        if (string.IsNullOrWhiteSpace(value)) return 0;
        foreach (var alias in aliases)
        {
            var candidate = NormalizeHeader(alias);
            if (value == candidate) return 96;
            if (value.Contains(candidate, StringComparison.OrdinalIgnoreCase) || candidate.Contains(value, StringComparison.OrdinalIgnoreCase)) return 82;
        }
        var headerTokens = Tokenize(header);
        var targetTokens = Tokenize(target);
        var overlap = headerTokens.Intersect(targetTokens, StringComparer.OrdinalIgnoreCase).Count();
        if (overlap > 0) return 74 + Math.Min(overlap * 4, 12);

        var closest = aliases.Select(alias => Similarity(value, NormalizeHeader(alias))).Max();
        return closest >= 0.78 ? 76 : closest >= 0.68 ? 70 : 0;
    }

    private static string NormalizeHeader(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static IReadOnlyList<string> Tokenize(string value) => value
        .Replace("_", " ", StringComparison.Ordinal)
        .Replace("-", " ", StringComparison.Ordinal)
        .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .SelectMany(token => System.Text.RegularExpressions.Regex.Split(token, "(?<=[a-z])(?=[A-Z])"))
        .Select(token => NormalizeHeader(token))
        .Where(token => token.Length > 1)
        .ToArray();

    private static double Similarity(string left, string right)
    {
        if (left.Length == 0 || right.Length == 0) return 0;
        var distance = new int[left.Length + 1, right.Length + 1];
        for (var i = 0; i <= left.Length; i++) distance[i, 0] = i;
        for (var j = 0; j <= right.Length; j++) distance[0, j] = j;
        for (var i = 1; i <= left.Length; i++)
            for (var j = 1; j <= right.Length; j++)
                distance[i, j] = Math.Min(Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1), distance[i - 1, j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1));
        return 1d - (double)distance[left.Length, right.Length] / Math.Max(left.Length, right.Length);
    }
}
