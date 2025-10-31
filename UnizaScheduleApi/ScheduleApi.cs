using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using KST.UnizaSchedule.Api.ScheduleRequests;

namespace KST.UnizaSchedule.Api
{
	public static class ScheduleApi
	{
		private const string URL = "https://nic.uniza.sk/webservices/";
		
		public static async Task<IEnumerable<ScheduleContent>> GetUnizaScheduleContentAsync(ScheduleRequest scheduleRequest)
		{
			using var httpClient = new HttpClient();

			var url = new UriBuilder(URL);
			url.Path += "getUnizaScheduleContent.php";
			url.Query = ScheduleApiHelpers.BuildQuery(scheduleRequest);

			var response = await httpClient.GetAsync(url.Uri);
			var responseObject = await JsonSerializer.DeserializeAsync<ScheduleContentResponse>(await response.Content.ReadAsStreamAsync());

			if (responseObject.Report != null)
				throw new InvalidOperationException($"Error getting the schedule: {responseObject.Report}");

			return responseObject
				.ScheduleContent
				.OrderBy(x => x.Day)
				.ThenBy(x => x.BlockNumber)
				.Distinct();
		}

        public static async Task<IEnumerable<UnizaTeacher>> GetUnizaTeachers(string query, CancellationToken ct)
        {
            var unizaUrl = "https://vzdelavanie.uniza.sk/vzdelavanie/rozvrh_search2.php?qs=1&q=" + HttpUtility.UrlEncode(query);
            using var httpClient = new HttpClient();

            var response = await httpClient.GetAsync("https://corsproxy.io?url="+ HttpUtility.UrlEncode(unizaUrl), ct);
            response.EnsureSuccessStatusCode();
            var responseObject = await JsonSerializer.DeserializeAsync<SearchApiResponse[]>(await response.Content.ReadAsStreamAsync(), cancellationToken: ct);
            return responseObject.Select(x => new UnizaTeacher(
                x.value.Replace("rozvrh2.php?sq=1&id=", ""),
                x.label,
                x.desc));
        }

        public static async Task<IEnumerable<String>> GetUnizaSchoolGroups(string query, CancellationToken ct)
        {
            var unizaUrl = "https://vzdelavanie.uniza.sk/vzdelavanie/rozvrh_search2.php?qs=2&q=" + HttpUtility.UrlEncode(query);
            using var httpClient = new HttpClient();

            var response = await httpClient.GetAsync("https://corsproxy.io?url=" + HttpUtility.UrlEncode(unizaUrl), ct);
            response.EnsureSuccessStatusCode();
            var responseObject = await JsonSerializer.DeserializeAsync<SearchApiResponse[]>(await response.Content.ReadAsStreamAsync(), cancellationToken: ct);
            return responseObject.Select(x => x.label);
        }

        public static async Task<IEnumerable<UnizaRoom>> GetUnizaRooms(string query, CancellationToken ct)
        {
            var unizaUrl = "https://vzdelavanie.uniza.sk/vzdelavanie/rozvrh_search2.php?qs=3&q=" + HttpUtility.UrlEncode(query);
            using var httpClient = new HttpClient();

            var response = await httpClient.GetAsync("https://corsproxy.io?url=" + HttpUtility.UrlEncode(unizaUrl), ct);
            response.EnsureSuccessStatusCode();
            var responseObject = await JsonSerializer.DeserializeAsync<SearchApiResponse[]>(await response.Content.ReadAsStreamAsync(), cancellationToken: ct);
            return responseObject.Select(x => new UnizaRoom(
                x.value.Replace("rozvrh2.php?sq=3&id=", ""),
                x.label,
                x.desc));
        }

        private record SearchApiResponse(string value, string label, string desc);
    }
}
