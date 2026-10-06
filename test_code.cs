using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PolicyCompliantApplication
{
    public class ApplicationSettings
    {
        public string ApplicationName { get; set; } = "DocumentProcessor";
        public int MaxDocumentSize { get; set; } = 5 * 1024 * 1024;
        public string LogDirectory { get; set; } = "logs";
        public int RequestTimeoutSeconds { get; set; } = 30;

        public string ExternalServiceUsername { get; set; } = string.Empty;
        public string ExternalServicePassword { get; set; } = string.Empty;
        public string ExternalServiceBaseUrl { get; set; } =
            "https://example.invalid";
    }

    public class DocumentRequest
    {
        public string DocumentName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string RequestedBy { get; set; } = string.Empty;
    }

    public class DocumentResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int CharacterCount { get; set; }
        public DateTime ProcessedAtUtc { get; set; }
    }

    public class ConfigurationLoader
    {
        public ApplicationSettings Load()
        {
            var settings = new ApplicationSettings();

            var maxSize =
                Environment.GetEnvironmentVariable(
                    "DOCUMENT_MAX_SIZE");

            if (!string.IsNullOrWhiteSpace(maxSize) &&
                int.TryParse(maxSize, out var parsedSize) &&
                parsedSize > 0 &&
                parsedSize <= 100 * 1024 * 1024)
            {
                settings.MaxDocumentSize = parsedSize;
            }

            var timeout =
                Environment.GetEnvironmentVariable(
                    "DOCUMENT_REQUEST_TIMEOUT");

            if (!string.IsNullOrWhiteSpace(timeout) &&
                int.TryParse(timeout, out var parsedTimeout) &&
                parsedTimeout >= 1 &&
                parsedTimeout <= 300)
            {
                settings.RequestTimeoutSeconds =
                    parsedTimeout;
            }

            var logDirectory =
                Environment.GetEnvironmentVariable(
                    "DOCUMENT_LOG_DIRECTORY");

            if (!string.IsNullOrWhiteSpace(logDirectory) &&
                logDirectory.Length <= 500)
            {
                settings.LogDirectory = logDirectory;
            }

            // SEC-001:
            // Credentials are loaded from the environment instead of
            // being embedded in source code.
            var username =
                Environment.GetEnvironmentVariable(
                    "DOCUMENT_SERVICE_USERNAME");

            var password =
                Environment.GetEnvironmentVariable(
                    "DOCUMENT_SERVICE_PASSWORD");

            if (string.IsNullOrWhiteSpace(username))
            {
                throw new InvalidOperationException(
                    "DOCUMENT_SERVICE_USERNAME is not configured.");
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException(
                    "DOCUMENT_SERVICE_PASSWORD is not configured.");
            }

            settings.ExternalServiceUsername = username;
            settings.ExternalServicePassword = password;

            var serviceUrl =
                Environment.GetEnvironmentVariable(
                    "DOCUMENT_SERVICE_URL");

            if (!string.IsNullOrWhiteSpace(serviceUrl) &&
                Uri.TryCreate(
                    serviceUrl,
                    UriKind.Absolute,
                    out var parsedUri) &&
                (parsedUri.Scheme == Uri.UriSchemeHttps))
            {
                settings.ExternalServiceBaseUrl =
                    parsedUri.ToString().TrimEnd('/');
            }

            return settings;
        }
    }

    public class DocumentProcessor
    {
        private readonly ApplicationSettings _settings;

        public DocumentProcessor(
            ApplicationSettings settings)
        {
            _settings = settings
                ?? throw new ArgumentNullException(
                    nameof(settings));
        }

        public DocumentResult Process(
            DocumentRequest request)
        {
            if (request == null)
            {
                return Failure(
                    "Request cannot be null.");
            }

            if (string.IsNullOrWhiteSpace(
                request.DocumentName))
            {
                return Failure(
                    "Document name is required.");
            }

            if (request.DocumentName.Length > 255)
            {
                return Failure(
                    "Document name is too long.");
            }

            if (request.Content == null)
            {
                return Failure(
                    "Document content is required.");
            }

            if (request.Content.Length >
                _settings.MaxDocumentSize)
            {
                return Failure(
                    "Document is too large.");
            }

            if (string.IsNullOrWhiteSpace(
                request.RequestedBy))
            {
                return Failure(
                    "Requester is required.");
            }

            if (request.RequestedBy.Length > 200)
            {
                return Failure(
                    "Requester value is too long.");
            }

            return new DocumentResult
            {
                Success = true,
                Message =
                    "Document processed successfully.",
                CharacterCount =
                    request.Content.Length,
                ProcessedAtUtc =
                    DateTime.UtcNow
            };
        }

        private static DocumentResult Failure(
            string message)
        {
            return new DocumentResult
            {
                Success = false,
                Message = message,
                ProcessedAtUtc = DateTime.UtcNow
            };
        }
    }

    public class AuditLogger
    {
        private readonly string _directory;

        public AuditLogger(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException(
                    "Log directory is required.",
                    nameof(directory));
            }

            _directory = directory;

            if (!DirectoryExists(_directory))
            {
                System.IO.Directory.CreateDirectory(
                    _directory);
            }
        }

        public void Write(
            string eventName,
            string details)
        {
            if (string.IsNullOrWhiteSpace(eventName))
            {
                throw new ArgumentException(
                    "Event name cannot be empty.",
                    nameof(eventName));
            }

            if (eventName.Length > 100)
            {
                throw new ArgumentException(
                    "Event name is too long.",
                    nameof(eventName));
            }

            details ??= string.Empty;

            var safeEventName =
                SanitizeFileName(eventName);

            var fileName =
                System.IO.Path.Combine(
                    _directory,
                    $"{safeEventName}.log");

            var line =
                $"{DateTime.UtcNow:O} | {details}";

            System.IO.File.AppendAllText(
                fileName,
                line + Environment.NewLine);
        }

        private static string SanitizeFileName(
            string value)
        {
            var invalid =
                System.IO.Path.GetInvalidFileNameChars();

            var builder =
                new StringBuilder(value.Length);

            foreach (var character in value)
            {
                if (Array.IndexOf(
                        invalid,
                        character) >= 0)
                {
                    builder.Append('_');
                }
                else
                {
                    builder.Append(character);
                }
            }

            return builder.ToString();
        }

        private static bool DirectoryExists(
            string path)
        {
            return System.IO.Directory.Exists(path);
        }
    }

    public class ExternalServiceClient
    {
        private readonly HttpClient _client;
        private readonly string _username;
        private readonly string _password;
        private readonly Uri _baseUri;

        public ExternalServiceClient(
            ApplicationSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(
                    nameof(settings));
            }

            if (string.IsNullOrWhiteSpace(
                settings.ExternalServiceUsername))
            {
                throw new InvalidOperationException(
                    "External service username is not configured.");
            }

            if (string.IsNullOrWhiteSpace(
                settings.ExternalServicePassword))
            {
                throw new InvalidOperationException(
                    "External service password is not configured.");
            }

            if (!Uri.TryCreate(
                    settings.ExternalServiceBaseUrl,
                    UriKind.Absolute,
                    out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidOperationException(
                    "External service URL must be a valid HTTPS URL.");
            }

            _username =
                settings.ExternalServiceUsername;

            _password =
                settings.ExternalServicePassword;

            _baseUri = uri;

            _client = new HttpClient
            {
                Timeout =
                    TimeSpan.FromSeconds(
                        settings.RequestTimeoutSeconds)
            };
        }

        public async Task<string>
            GetRemoteDocumentAsync(
                string documentId,
                CancellationToken cancellationToken)
        {
            // SEC-003:
            // Validate user-controlled document ID before use.
            if (string.IsNullOrWhiteSpace(documentId))
            {
                throw new ArgumentException(
                    "Document ID is required.",
                    nameof(documentId));
            }

            if (documentId.Length > 100)
            {
                throw new ArgumentException(
                    "Document ID is too long.",
                    nameof(documentId));
            }

            var encodedId =
                Uri.EscapeDataString(documentId);

            var requestUri =
                new Uri(
                    _baseUri,
                    $"/api/documents/{encodedId}");

            using var request =
                new HttpRequestMessage(
                    HttpMethod.Get,
                    requestUri);

            var credentials =
                Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(
                        $"{_username}:{_password}"));

            request.Headers.Authorization =
                new System.Net.Http.Headers
                    .AuthenticationHeaderValue(
                        "Basic",
                        credentials);

            using var response =
                await _client.SendAsync(
                    request,
                    cancellationToken);

            response.EnsureSuccessStatusCode();

            return await response.Content
                .ReadAsStringAsync(
                    cancellationToken);
        }
    }

    public class DocumentRepository
    {
        private readonly Dictionary<string, string>
            _documents =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase);

        public void Save(
            string documentId,
            string content)
        {
            ValidateDocumentId(documentId);

            if (content == null)
            {
                throw new ArgumentNullException(
                    nameof(content));
            }

            if (content.Length >
                10 * 1024 * 1024)
            {
                throw new ArgumentException(
                    "Document is too large.",
                    nameof(content));
            }

            _documents[documentId] = content;
        }

        public bool TryGet(
            string documentId,
            out string content)
        {
            content = string.Empty;

            if (!IsValidDocumentId(documentId))
            {
                return false;
            }

            return _documents.TryGetValue(
                documentId,
                out content);
        }

        public bool Delete(
            string documentId)
        {
            if (!IsValidDocumentId(documentId))
            {
                return false;
            }

            return _documents.Remove(documentId);
        }

        public IReadOnlyCollection<string>
            GetDocumentIds()
        {
            return _documents.Keys;
        }

        private static void ValidateDocumentId(
            string documentId)
        {
            if (!IsValidDocumentId(documentId))
            {
                throw new ArgumentException(
                    "Invalid document ID.",
                    nameof(documentId));
            }
        }

        private static bool IsValidDocumentId(
            string documentId)
        {
            return
                !string.IsNullOrWhiteSpace(documentId) &&
                documentId.Length <= 100;
        }
    }

    public class ApplicationService
    {
        private readonly DocumentProcessor _processor;
        private readonly DocumentRepository _repository;
        private readonly AuditLogger _logger;

        public ApplicationService(
            DocumentProcessor processor,
            DocumentRepository repository,
            AuditLogger logger)
        {
            _processor =
                processor
                ?? throw new ArgumentNullException(
                    nameof(processor));

            _repository =
                repository
                ?? throw new ArgumentNullException(
                    nameof(repository));

            _logger =
                logger
                ?? throw new ArgumentNullException(
                    nameof(logger));
        }

        public DocumentResult CreateDocument(
            string id,
            DocumentRequest request)
        {
            if (!IsValidDocumentId(id))
            {
                return Failure(
                    "Invalid document ID.");
            }

            var result =
                _processor.Process(request);

            if (!result.Success)
            {
                _logger.Write(
                    "document-errors",
                    result.Message);

                return result;
            }

            _repository.Save(
                id,
                request.Content);

            _logger.Write(
                "document-created",
                $"Document '{id}' was created.");

            result.Message =
                "Document created successfully.";

            return result;
        }

        public bool DeleteDocument(
            string id)
        {
            if (!IsValidDocumentId(id))
            {
                return false;
            }

            var deleted =
                _repository.Delete(id);

            if (deleted)
            {
                _logger.Write(
                    "document-deleted",
                    $"Document '{id}' was deleted.");
            }

            return deleted;
        }

        private static bool IsValidDocumentId(
            string id)
        {
            return
                !string.IsNullOrWhiteSpace(id) &&
                id.Length <= 100;
        }

        private static DocumentResult Failure(
            string message)
        {
            return new DocumentResult
            {
                Success = false,
                Message = message,
                ProcessedAtUtc =
                    DateTime.UtcNow
            };
        }
    }

    public class CommandLineInterface
    {
        private readonly ApplicationService _service;

        public CommandLineInterface(
            ApplicationService service)
        {
            _service =
                service
                ?? throw new ArgumentNullException(
                    nameof(service));
        }

        public void Run()
        {
            Console.WriteLine(
                "Document Processor");

            Console.WriteLine(
                "==================");

            Console.WriteLine(
                "Commands:");

            Console.WriteLine(
                "  create");

            Console.WriteLine(
                "  delete");

            Console.WriteLine(
                "  exit");

            while (true)
            {
                Console.WriteLine();
                Console.Write("> ");

                var command =
                    Console.ReadLine();

                if (command == null)
                {
                    break;
                }

                command =
                    command.Trim()
                        .ToLowerInvariant();

                switch (command)
                {
                    case "create":
                        CreateDocument();
                        break;

                    case "delete":
                        DeleteDocument();
                        break;

                    case "exit":
                        return;

                    default:
                        Console.WriteLine(
                            "Unknown command.");
                        break;
                }
            }
        }

        private void CreateDocument()
        {
            Console.Write("Document ID: ");

            var id =
                Console.ReadLine();

            Console.Write("Document name: ");

            var name =
                Console.ReadLine();

            Console.Write("Requested by: ");

            var requestedBy =
                Console.ReadLine();

            Console.WriteLine(
                "Enter document content:");

            var content =
                Console.ReadLine();

            var request =
                new DocumentRequest
                {
                    DocumentName =
                        name ?? string.Empty,

                    RequestedBy =
                        requestedBy ?? string.Empty,

                    Content =
                        content ?? string.Empty
                };

            var result =
                _service.CreateDocument(
                    id ?? string.Empty,
                    request);

            Console.WriteLine(
                result.Message);
        }

        private void DeleteDocument()
        {
            Console.Write(
                "Document ID: ");

            var id =
                Console.ReadLine();

            if (string.IsNullOrWhiteSpace(id))
            {
                Console.WriteLine(
                    "Document ID is required.");

                return;
            }

            var deleted =
                _service.DeleteDocument(id);

            Console.WriteLine(
                deleted
                    ? "Document deleted."
                    : "Document was not found.");
        }
    }

    public static class Program
    {
        public static void Main(
            string[] args)
        {
            try
            {
                var configurationLoader =
                    new ConfigurationLoader();

                var settings =
                    configurationLoader.Load();

                var logger =
                    new AuditLogger(
                        settings.LogDirectory);

                logger.Write(
                    "application",
                    "Application starting.");

                var repository =
                    new DocumentRepository();

                var processor =
                    new DocumentProcessor(
                        settings);

                var service =
                    new ApplicationService(
                        processor,
                        repository,
                        logger);

                var externalClient =
                    new ExternalServiceClient(
                        settings);

                // The client is constructed successfully using
                // externally supplied credentials.
                _ = externalClient;

                var cli =
                    new CommandLineInterface(
                        service);

                cli.Run();

                logger.Write(
                    "application",
                    "Application stopped normally.");
            }
            catch (InvalidOperationException ex)
            {
                Console.Error.WriteLine(
                    $"Configuration error: {ex.Message}");
            }
            catch (Exception)
            {
                Console.Error.WriteLine(
                    "An unexpected application error occurred.");
            }
        }
    }
}
