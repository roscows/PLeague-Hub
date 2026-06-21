using System.Linq.Expressions;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;

namespace PLeagueHub.Api.Tests;

public sealed class ContentEndpointsTests
{
    [Fact]
    public async Task GetNews_ReturnsOnlyVisibleNewsPosts()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var news = await client.GetFromJsonAsync<List<Post>>("/api/news");

        Assert.NotNull(news);
        Assert.Single(news);
        Assert.Equal("news-1", news[0].Id);
        Assert.Equal("vest", news[0].Tip);
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IRepository<Post>>();
                    services.AddSingleton<IRepository<Post>>(new FakeRepository<Post>(
                    [
                        new Post
                        {
                            Id = "news-1",
                            AutorId = "665000000000000000000501",
                            Naslov = "Derbi kola",
                            Sadrzaj = "Najava derbija kola.",
                            Tip = "vest",
                            DatumKreiranja = new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc),
                            Obrisan = false
                        },
                        new Post
                        {
                            Id = "discussion-1",
                            AutorId = "665000000000000000000501",
                            Naslov = "Ko osvaja titulu?",
                            Sadrzaj = "Diskusija o kandidatima za titulu.",
                            Tip = "diskusija",
                            DatumKreiranja = new DateTime(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc),
                            Obrisan = false
                        },
                        new Post
                        {
                            Id = "deleted-news",
                            AutorId = "665000000000000000000501",
                            Naslov = "Obrisana vest",
                            Sadrzaj = "Ovaj post ne treba da se vidi.",
                            Tip = "vest",
                            DatumKreiranja = new DateTime(2026, 8, 12, 12, 0, 0, DateTimeKind.Utc),
                            Obrisan = true
                        },
                        new Post
                        {
                            Id = "deleted-discussion",
                            AutorId = "665000000000000000000501",
                            Naslov = "Obrisana diskusija",
                            Sadrzaj = "Ovaj post ne treba da se vidi.",
                            Tip = "diskusija",
                            DatumKreiranja = new DateTime(2026, 8, 13, 12, 0, 0, DateTimeKind.Utc),
                            Obrisan = true
                        }
                    ]));
                });
            });
    }

    private sealed class FakeRepository<TDocument> : IRepository<TDocument>
        where TDocument : BaseDocument
    {
        private readonly List<TDocument> _documents;

        public FakeRepository(IEnumerable<TDocument> documents)
        {
            _documents = documents.ToList();
        }

        public Task<IReadOnlyCollection<TDocument>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<TDocument>>(_documents);
        }

        public Task<TDocument?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_documents.FirstOrDefault(document => document.Id == id));
        }

        public Task<TDocument?> FindOneAsync(
            Expression<Func<TDocument, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_documents.AsQueryable().FirstOrDefault(predicate));
        }

        public Task<bool> ExistsAsync(
            Expression<Func<TDocument, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_documents.AsQueryable().Any(predicate));
        }

        public Task<TDocument> CreateAsync(TDocument document, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> UpdateAsync(string id, TDocument document, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
