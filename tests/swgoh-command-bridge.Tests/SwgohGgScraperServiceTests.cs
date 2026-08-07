#nullable enable

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using swgoh_command_bridge.Core.Database;
using swgoh_command_bridge.Core.Database.Entities;
using swgoh_command_bridge.Core.Models;
using swgoh_command_bridge.Core.Services;
using Xunit;

namespace swgoh_command_bridge.Tests
{
    /// <summary>
    /// Fixture-based unit tests for SwgohGgScraperService parsing logic.
    /// </summary>
    public class SwgohGgScraperServiceTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly AppDbContext _context;

        public SwgohGgScraperServiceTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options;

            _context = new AppDbContext(options);
            _context.Database.EnsureCreated();
        }

        [Fact]
        public async Task ScrapeCharacterRecommendationsAsync_WithValidHtmlFixture_ParsesAndPersistsCorrectly()
        {
            // Arrange
            var html = @"
                <div class=""mod-set-image"" alt=""Speed""></div>
                <div class=""mod-set-percent"">62.5%</div>
                <div class=""mod-set-image"" alt=""Health""></div>
                <div class=""mod-set-percent"">37.5%</div>
                
                Slot 2
                <div class=""mod-stat-name"">Speed</div>
                <div class=""mod-stat-percent"">95.2%</div>
                
                Slot 4
                <div class=""mod-stat-name"">Critical Damage</div>
                <div class=""mod-stat-percent"">78.1%</div>
            ";

            var handler = new FakeHttpMessageHandler(req =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(html)
                };
                return Task.FromResult(response);
            });

            var httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://swgoh.gg")
            };
            var clientFactory = new FakeHttpClientFactory(httpClient);

            var scraper = new SwgohGgScraperService(clientFactory, _context, NullLogger<SwgohGgScraperService>.Instance);

            // Act
            var success = await scraper.ScrapeCharacterRecommendationsAsync("DARTHTRAYA", CancellationToken.None);

            // Assert
            Assert.True(success);

            var persisted = await _context.SwgohGgRecommendations
                .FirstOrDefaultAsync(r => r.CharacterId == "DARTHTRAYA");

            Assert.NotNull(persisted);
            Assert.Contains("Speed", persisted!.SetRecommendationsJson);
            Assert.Contains("Health", persisted.SetRecommendationsJson);
            Assert.Contains("Speed", persisted.PrimaryStatsJson);
            Assert.Contains("Critical Damage", persisted.PrimaryStatsJson);
            Assert.Equal("swgoh.gg", persisted.Source);
            Assert.Equal(1, persisted.RecommendationSchemaVersion);
            Assert.Contains("/characters/darthtraya/best-mods/", persisted.SourceUrl);

            var snapshot = RecommendationSnapshot.FromEntity(persisted);
            Assert.Equal("swgoh.gg", snapshot.Source);
            Assert.Contains(snapshot.Sets, set => set.Name == "Speed");
            Assert.Contains("Speed", snapshot.PrimaryStats.Keys);
        }

        [Fact]
        public async Task HasRecommendationAsync_WhenRecommendationDoesNotExist_ReturnsFalse()
        {
            // Arrange
            var clientFactory = new FakeHttpClientFactory(new HttpClient());
            var scraper = new SwgohGgScraperService(clientFactory, _context, NullLogger<SwgohGgScraperService>.Instance);

            // Act
            var exists = await scraper.HasRecommendationAsync("MISSING_CHAR", CancellationToken.None);

            // Assert
            Assert.False(exists);
        }

        [Fact]
        public async Task HasRecommendationAsync_WhenFreshRecommendationExists_ReturnsTrue()
        {
            // Arrange
            var clientFactory = new FakeHttpClientFactory(new HttpClient());
            var scraper = new SwgohGgScraperService(clientFactory, _context, NullLogger<SwgohGgScraperService>.Instance);

            var recommendation = new SwgohGgRecommendationEntity
            {
                CharacterId = "EXISTING_CHAR",
                LastUpdatedUtc = DateTime.UtcNow.AddDays(-2) // fresh (< 7 days)
            };
            _context.SwgohGgRecommendations.Add(recommendation);
            await _context.SaveChangesAsync();

            // Act
            var exists = await scraper.HasRecommendationAsync("EXISTING_CHAR", CancellationToken.None);

            // Assert
            Assert.True(exists);
        }

        [Fact]
        public async Task ScrapeCharacterRecommendationsAsync_PropagatesCallerCancellation()
        {
            var clientFactory = new FakeHttpClientFactory(new HttpClient());
            var scraper = new SwgohGgScraperService(
                clientFactory,
                _context,
                NullLogger<SwgohGgScraperService>.Instance);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                scraper.ScrapeCharacterRecommendationsAsync("DARTHTRAYA", cancellation.Token));
        }

        [Fact]
        public async Task ScrapeCharacterRecommendationsAsync_WithEmptyOrInvalidHtml_ReturnsFalseAndDoesNotPersist()
        {
            // Arrange
            var html = "<html><body>No recommendations here!</body></html>";

            var handler = new FakeHttpMessageHandler(req =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(html)
                };
                return Task.FromResult(response);
            });

            var httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://swgoh.gg")
            };
            var clientFactory = new FakeHttpClientFactory(httpClient);

            var scraper = new SwgohGgScraperService(clientFactory, _context, NullLogger<SwgohGgScraperService>.Instance);

            // Act
            var success = await scraper.ScrapeCharacterRecommendationsAsync("DARTHTRAYA", CancellationToken.None);

            // Assert
            Assert.False(success);

            var persisted = await _context.SwgohGgRecommendations
                .FirstOrDefaultAsync(r => r.CharacterId == "DARTHTRAYA");

            Assert.Null(persisted);
        }

        [Fact]
        public async Task ScrapeAllCharactersIncrementalAsync_ReportsProgressForRequestedAllyCode()
        {
            _context.Players.Add(new PlayerEntity
            {
                AllyCode = "123456789",
                Name = "Test Player",
                Characters = new List<CharacterEntity>
                {
                    new()
                    {
                        Id = "DARTHTRAYA",
                        PlayerAllyCode = "123456789",
                        Name = "Darth Traya"
                    }
                }
            });
            await _context.SaveChangesAsync();

            _context.Players.Add(new PlayerEntity
            {
                AllyCode = "987654321",
                Name = "Other Player",
                Characters = new List<CharacterEntity>
                {
                    new()
                    {
                        Id = "OTHER_CHARACTER",
                        PlayerAllyCode = "987654321",
                        Name = "Other Character"
                    }
                }
            });
            await _context.SaveChangesAsync();

            var handler = new FakeHttpMessageHandler(req =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("<div class=\"mod-set-image\" alt=\"Speed\"></div><div class=\"mod-set-percent\">60%</div>")
                };
                return Task.FromResult(response);
            });
            var clientFactory = new FakeHttpClientFactory(new HttpClient(handler));
            var scraper = new SwgohGgScraperService(
                clientFactory,
                _context,
                NullLogger<SwgohGgScraperService>.Instance);
            var updates = new List<ScrapeProgress>();

            await scraper.ScrapeAllCharactersIncrementalAsync(
                new Progress<ScrapeProgress>(updates.Add),
                CancellationToken.None,
                "123456789");

            var update = Assert.Single(updates);
            Assert.Equal(1, update.Current);
            Assert.Equal(1, update.Total);
            Assert.True(update.Success);
        }

        public void Dispose()
        {
            _context.Dispose();
            _connection.Dispose();
        }
    }

    public class FakeHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public FakeHttpClientFactory(HttpClient client) => _client = client;

        public HttpClient CreateClient(string name) => _client;
    }

    public class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) => _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handler(request);
        }
    }
}
