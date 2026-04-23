#pragma warning disable SKEXP0070
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Text;
using UglyToad.PdfPig;

namespace SmartDocQA.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class DocumentController : ControllerBase
	{
		private readonly IConfiguration _configuration;
		private readonly HttpClient _httpClient;

		public DocumentController(IConfiguration configuration, IHttpClientFactory httpClientFactory)
		{
			_configuration = configuration;
			_httpClient = httpClientFactory.CreateClient();
		}

		[HttpPost("upload-and-ask")]
		public async Task<IActionResult> UploadAndAsk(
			IFormFile file, [FromForm] string question)
		{
			if (file == null || file.Length == 0)
				return BadRequest("PDF file required");

			if (string.IsNullOrEmpty(question))
				return BadRequest("Question required");

			// PDF se text extract karo
			string extractedText = "";
			using (var stream = file.OpenReadStream())
			{
				using var pdf = PdfDocument.Open(stream);
				foreach (var page in pdf.GetPages())
				{
					extractedText += page.Text + " ";
				}
			}

			// Groq API call
			var apiKey = _configuration["GroqApiKey"];
			var requestBody = new
			{
				model = "llama3-8b-8192",
				messages = new[]
				{
					new {
						role = "user",
						content = $"Neeche document ka content hai:\n{extractedText}\n\nSawaal: {question}\n\nDocument ke basis par jawab do."
					}
				}
			};

			var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
			request.Headers.Add("Authorization", $"Bearer {apiKey}");
			request.Content = new StringContent(
				JsonConvert.SerializeObject(requestBody),
				Encoding.UTF8,
				"application/json"
			);

			var httpResponse = await _httpClient.SendAsync(request);
			var responseString = await httpResponse.Content.ReadAsStringAsync();

			if (!httpResponse.IsSuccessStatusCode)
				return StatusCode((int)httpResponse.StatusCode, responseString);

			dynamic responseJson = JsonConvert.DeserializeObject(responseString)!;
			string answer = responseJson.choices[0].message.content;

			return Ok(new
			{
				question = question,
				answer = answer
			});
		}
	}
}