using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PolicyTestApplication
{

    public class ApplicationSettings
    {
        public string ApplicationName { get; set; } = "DocumentProcessor";
        public int MaxDocumentSize { get; set; } = 5 * 1024 * 1024;
        public string LogDirectory { get; set; } = "logs";
        public int RequestTimeoutSeconds { get; set; } = 30;
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

    public class DocumentProcessor
    {
        private readonly ApplicationSettings _settings;

        public DocumentProcessor(ApplicationSettings settings)
        {
            _settings = settings;
        }

        public DocumentResult Process(DocumentRequest request)
        {
            if (request == null)
            {
                return new DocumentResult
                {
                    Success = false,
                    Message = "Request cannot be null.",
                    ProcessedAtUtc = DateTime.UtcNow
                };
            }

            if (string.IsNullOrWhiteSpace(request.DocumentName))
            {
                return new DocumentResult
                {
                    Success = false,
                    Message = "Document name is required.",
                    ProcessedAtUtc = DateTime.UtcNow
                };
            }

            if (request.Content == null)
            {
                return new DocumentResult
                {
                    Success = false,
                    Message = "Document content is required.",
                    ProcessedAtUtc = DateTime.UtcNow
                };
            }

            if (request.Content.Length > _settings.MaxDocumentSize)
            {
                return new DocumentResult
                {
                    Success = false,
                    Message = "Document is too large.",
                    ProcessedAtUtc = DateTime.UtcNow
                };
            }

            if (request.RequestedBy == null ||
                request.RequestedBy.Length > 200)
            {
                return new DocumentResult
                {
                    Success = false,
                    Message = "Invalid requester.",
                    ProcessedAtUtc = DateTime.UtcNow
                };
            }

            var normalizedName = request.DocumentName.Trim();

            if (normalizedName.Length > 255)
            {
                return new DocumentResult
                {
                    Success = false,
                    Message = "Document name is too long.",
                    ProcessedAtUtc = DateTime.UtcNow
                };
            }

            Console.WriteLine(
                $"Processing document '{normalizedName}' " +
                $"requested by '{request.RequestedBy}'.");

            return new DocumentResult
            {
                Success = true,
                Message = "Document processed successfully.",
                CharacterCount = request.Content.Length,
                ProcessedAtUtc = DateTime.UtcNow
            };
        }
    }

    public class AuditLogger
    {
        private readonly string _directory;

        public AuditLogger(string directory)
        {
            _directory = directory;

            if (!Directory.Exists(_directory))
            {
                Directory.CreateDirectory(_directory);
            }
        }

        public void Write(string eventName, string details)
        {
            if (string.IsNullOrWhiteSpace(eventName))
            {
                throw new ArgumentException(
                    "Event name cannot be empty.",
                    nameof(eventName));
            }

            if (details == null)
            {
                details = string.Empty;
            }

            var safeEventName = eventName
                .Replace("/", "_")
                .Replace("\\", "_")
                .Replace(":", "_");

            var fileName =
                Path.Combine(
                    _directory,
                    $"{safeEventName}.log");

            var line =
                $"{DateTime.UtcNow:O} | {details}";

            File.AppendAllText(
                fileName,
                line + Environment.NewLine);
        }
    }

    public class ExternalServiceClient
    {
        private readonly HttpClient _client;

        /*
         * SEC-001 VIOLATION
         *
         * This credential is embedded directly in source code.
         *
         * It should instead be obtained from an environment variable,
         * secret-management service, or another approved configuration
         * mechanism.
         */
        private const string ApiUsername = "document-service";
        private const string ApiPassword = "SuperSecretPassword123!";

        public ExternalServiceClient()
        {
            _client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        public async Task<string> GetRemoteDocumentAsync(
            string documentId,
            CancellationToken cancellationToken)
        {
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

            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://example.invalid/api/documents/{Uri.EscapeDataString(documentId)}");

            var credentials =
                Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(
                        $"{ApiUsername}:{ApiPassword}"));

            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue(
                    "Basic",
                    credentials);

            using var response =
                await _client.SendAsync(
                    request,
                    cancellationToken);

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync(
                cancellationToken);
        }
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

            return settings;
        }
    }

    public class DocumentRepository
    {
        private readonly Dictionary<string, string> _documents =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

        public void Save(
            string documentId,
            string content)
        {
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

            if (content == null)
            {
                throw new ArgumentNullException(
                    nameof(content));
            }

            if (content.Length > 10 * 1024 * 1024)
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

            if (string.IsNullOrWhiteSpace(documentId))
            {
                return false;
            }

            if (documentId.Length > 100)
            {
                return false;
            }

            return _documents.TryGetValue(
                documentId,
                out content);
        }

        public bool Delete(string documentId)
        {
            if (string.IsNullOrWhiteSpace(documentId))
            {
                return false;
            }

            return _documents.Remove(documentId);
        }

        public IReadOnlyCollection<string> GetDocumentIds()
        {
            return _documents.Keys;
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
            _processor = processor;
            _repository = repository;
            _logger = logger;
        }

        public DocumentResult CreateDocument(
            string id,
            DocumentRequest request)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return new DocumentResult
                {
                    Success = false,
                    Message = "Document ID is required."
                };
            }

            if (id.Length > 100)
            {
                return new DocumentResult
                {
                    Success = false,
                    Message = "Document ID is too long."
                };
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

        public bool DeleteDocument(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            if (id.Length > 100)
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
    }

    public class CommandLineInterface
    {
        private readonly ApplicationService _service;

        public CommandLineInterface(
            ApplicationService service)
        {
            _service = service;
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

            if (string.IsNullOrWhiteSpace(id))
            {
                Console.WriteLine(
                    "Document ID is required.");

                return;
            }

            Console.Write("Document name: ");

            var name =
                Console.ReadLine();

            if (string.IsNullOrWhiteSpace(name))
            {
                Console.WriteLine(
                    "Document name is required.");

                return;
            }

            Console.Write("Requested by: ");

            var requestedBy =
                Console.ReadLine();

            if (string.IsNullOrWhiteSpace(requestedBy))
            {
                Console.WriteLine(
                    "Requester is required.");

                return;
            }

            Console.WriteLine(
                "Enter document content:");

            var content =
                Console.ReadLine();

            if (content == null)
            {
                Console.WriteLine(
                    "Content is required.");

                return;
            }

            var request =
                new DocumentRequest
                {
                    DocumentName = name,
                    RequestedBy = requestedBy,
                    Content = content
                };

            var result =
                _service.CreateDocument(
                    id,
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
        public static async Task Main(
            string[] args)
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

            var cli =
                new CommandLineInterface(
                    service);

            try
            {
                cli.Run();

                logger.Write(
                    "application",
                    "Application stopped normally.");
            }
            catch (Exception ex)
            {
                logger.Write(
                    "application-errors",
                    $"Unhandled exception: {ex.Message}");

                Console.Error.WriteLine(
                    "An unexpected error occurred.");
            }

            /*
             * The external client is instantiated here to demonstrate
             * another component of the application.
             */
            var externalClient =
                new ExternalServiceClient();

            /*
             * This call is intentionally not made automatically because
             * the example endpoint is non-functional.
             */
            _ = externalClient;

            await Task.CompletedTask;
        }
    }
}
