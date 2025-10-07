// 📁 src/GradoCerrado.Infrastructure/Services/DocumentExtractionService.cs
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace GradoCerrado.Infrastructure.Services;

/// <summary>
/// Servicio consolidado para extraer texto de diferentes formatos de documento
/// </summary>
public interface IDocumentExtractionService
{
	/// <summary>
	/// Extrae texto de un archivo según su extensión
	/// </summary>
	Task<string> ExtractTextFromFileAsync(Stream fileStream, string fileName);
}

public class DocumentExtractionService : IDocumentExtractionService
{
	private readonly ILogger<DocumentExtractionService> _logger;

	public DocumentExtractionService(ILogger<DocumentExtractionService> logger)
	{
		_logger = logger;
	}

	public async Task<string> ExtractTextFromFileAsync(Stream fileStream, string fileName)
	{
		var extension = Path.GetExtension(fileName).ToLower();

		return extension switch
		{
			".txt" or ".md" => await ExtractPlainTextAsync(fileStream),
			".pdf" => await ExtractPdfTextAsync(fileStream, fileName),
			".docx" => await ExtractDocxTextAsync(fileStream, fileName),
			_ => throw new NotSupportedException($"Formato no soportado: {extension}")
		};
	}

	// ═══════════════════════════════════════════════════════════
	// EXTRACCIÓN DE TEXTO PLANO
	// ═══════════════════════════════════════════════════════════

	private async Task<string> ExtractPlainTextAsync(Stream stream)
	{
		using var reader = new StreamReader(stream, Encoding.UTF8);
		var text = await reader.ReadToEndAsync();
		return CleanExtractedText(text);
	}

	// ═══════════════════════════════════════════════════════════
	// EXTRACCIÓN DE PDF (CONSOLIDADO CON ESTRATEGIAS)
	// ═══════════════════════════════════════════════════════════

	private async Task<string> ExtractPdfTextAsync(Stream stream, string fileName)
	{
		try
		{
			// Estrategia 1: LocationTextExtractionStrategy (más precisa)
			_logger.LogInformation("Extrayendo PDF con estrategia principal: {FileName}", fileName);
			return await ExtractPdfWithLocationStrategyAsync(stream);
		}
		catch (Exception primaryEx)
		{
			_logger.LogWarning(primaryEx, "Estrategia principal falló para {FileName}, usando fallback", fileName);

			try
			{
				// Estrategia 2: SimpleTextExtractionStrategy (más robusta)
				stream.Position = 0; // Reset para reutilizar
				return await ExtractPdfWithSimpleStrategyAsync(stream);
			}
			catch (Exception fallbackEx)
			{
				_logger.LogError(fallbackEx, "Todas las estrategias fallaron para {FileName}", fileName);
				throw new InvalidOperationException(
					$"No se pudo procesar el PDF '{fileName}'. " +
					$"Posibles causas: archivo protegido, dañado, o contiene solo imágenes. " +
					$"Error principal: {primaryEx.Message}. " +
					$"Error fallback: {fallbackEx.Message}",
					primaryEx);
			}
		}
	}

	private async Task<string> ExtractPdfWithLocationStrategyAsync(Stream stream)
	{
		using var pdfReader = new PdfReader(stream);
		using var pdfDocument = new PdfDocument(pdfReader);

		var textBuilder = new StringBuilder();
		var totalPages = pdfDocument.GetNumberOfPages();

		_logger.LogInformation("Procesando PDF con LocationStrategy: {TotalPages} páginas", totalPages);

		for (int i = 1; i <= totalPages; i++)
		{
			try
			{
				var page = pdfDocument.GetPage(i);
				var strategy = new LocationTextExtractionStrategy();
				var pageText = PdfTextExtractor.GetTextFromPage(page, strategy);

				if (!string.IsNullOrWhiteSpace(pageText))
				{
					textBuilder.AppendLine(pageText);
					_logger.LogDebug("Página {PageNumber}: {CharCount} caracteres extraídos", i, pageText.Length);
				}
				else
				{
					_logger.LogWarning("Página {PageNumber}: No se extrajo texto", i);
				}
			}
			catch (Exception pageEx)
			{
				_logger.LogWarning(pageEx, "Error en página {PageNumber}, omitiendo", i);
				textBuilder.AppendLine($"\n[Página {i}: Error de procesamiento - contenido omitido]\n");
			}
		}

		var extractedText = textBuilder.ToString();

		if (string.IsNullOrWhiteSpace(extractedText))
		{
			throw new InvalidOperationException("No se pudo extraer texto del PDF (LocationStrategy)");
		}

		_logger.LogInformation("PDF procesado: {CharCount} caracteres de {TotalPages} páginas",
			extractedText.Length, totalPages);

		return CleanExtractedText(extractedText);
	}

	private async Task<string> ExtractPdfWithSimpleStrategyAsync(Stream stream)
	{
		using var pdfReader = new PdfReader(stream);
		using var pdfDocument = new PdfDocument(pdfReader);

		var textBuilder = new StringBuilder();
		var totalPages = pdfDocument.GetNumberOfPages();

		_logger.LogInformation("Procesando PDF con SimpleStrategy (fallback): {TotalPages} páginas", totalPages);

		for (int i = 1; i <= totalPages; i++)
		{
			try
			{
				var page = pdfDocument.GetPage(i);
				var strategy = new SimpleTextExtractionStrategy();
				var pageText = PdfTextExtractor.GetTextFromPage(page, strategy);

				if (!string.IsNullOrWhiteSpace(pageText))
				{
					textBuilder.AppendLine(pageText);
				}
			}
			catch (Exception pageEx)
			{
				_logger.LogWarning(pageEx, "Página {PageNumber} omitida en fallback", i);
				textBuilder.AppendLine($"\n[Página {i}: Contenido no disponible]\n");
			}
		}

		var extractedText = textBuilder.ToString();

		if (string.IsNullOrWhiteSpace(extractedText))
		{
			throw new InvalidOperationException("No se pudo extraer texto del PDF (SimpleStrategy)");
		}

		_logger.LogInformation("PDF procesado con fallback: {CharCount} caracteres", extractedText.Length);

		return CleanExtractedText(extractedText);
	}

	// ═══════════════════════════════════════════════════════════
	// EXTRACCIÓN DE DOCX
	// ═══════════════════════════════════════════════════════════

	private async Task<string> ExtractDocxTextAsync(Stream stream, string fileName)
	{
		try
		{
			using var wordDocument = WordprocessingDocument.Open(stream, false);
			var body = wordDocument.MainDocumentPart?.Document?.Body;

			if (body == null)
			{
				throw new InvalidOperationException($"No se pudo acceder al contenido del DOCX: {fileName}");
			}

			var textBuilder = new StringBuilder();

			// Extraer párrafos
			foreach (var paragraph in body.Elements<Paragraph>())
			{
				var paragraphText = GetParagraphText(paragraph);
				if (!string.IsNullOrWhiteSpace(paragraphText))
				{
					textBuilder.AppendLine(paragraphText);
				}
			}

			// Extraer tablas
			foreach (var table in body.Elements<Table>())
			{
				var tableText = GetTableText(table);
				if (!string.IsNullOrWhiteSpace(tableText))
				{
					textBuilder.AppendLine(tableText);
				}
			}

			var extractedText = textBuilder.ToString();
			_logger.LogInformation("DOCX procesado: {CharCount} caracteres extraídos de {FileName}",
				extractedText.Length, fileName);

			return CleanExtractedText(extractedText);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error extrayendo texto de DOCX: {FileName}", fileName);
			throw new InvalidOperationException($"Error al procesar archivo DOCX: {fileName}", ex);
		}
	}

	private string GetParagraphText(Paragraph paragraph)
	{
		var textBuilder = new StringBuilder();
		foreach (var run in paragraph.Elements<Run>())
		{
			foreach (var text in run.Elements<Text>())
			{
				textBuilder.Append(text.Text);
			}
		}
		return textBuilder.ToString();
	}

	private string GetTableText(Table table)
	{
		var textBuilder = new StringBuilder();

		foreach (var row in table.Elements<TableRow>())
		{
			var rowTexts = new List<string>();

			foreach (var cell in row.Elements<TableCell>())
			{
				var cellText = new StringBuilder();
				foreach (var paragraph in cell.Elements<Paragraph>())
				{
					cellText.Append(GetParagraphText(paragraph));
				}
				rowTexts.Add(cellText.ToString().Trim());
			}

			if (rowTexts.Any(t => !string.IsNullOrWhiteSpace(t)))
			{
				textBuilder.AppendLine(string.Join(" | ", rowTexts));
			}
		}

		return textBuilder.ToString();
	}

	// ═══════════════════════════════════════════════════════════
	// LIMPIEZA DE TEXTO
	// ═══════════════════════════════════════════════════════════

	private string CleanExtractedText(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
			return string.Empty;

		// Normalizar espacios en blanco
		text = Regex.Replace(text, @"\s+", " ");

		// Remover caracteres de control
		text = Regex.Replace(text, @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", "");

		return text.Trim();
	}
}