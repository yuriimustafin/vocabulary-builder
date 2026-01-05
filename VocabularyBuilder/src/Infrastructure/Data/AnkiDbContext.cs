using System.Reflection;
using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Domain.Samples.Entities;
using VocabularyBuilder.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using VocabularyBuilder.Domain.Samples.Entities.ImportedBook;
using VocabularyBuilder.Domain.Entities.Frequency;
using System.Reflection.Emit;
using VocabularyBuilder.Domain.Entities.Anki;

namespace VocabularyBuilder.Infrastructure.Data;

//public class AnkiDbContext : DbContext, IAnkiDbContext
//{
//    public AnkiDbContext(DbContextOptions<AnkiDbContext> options) : base(options) { }

//    protected override void OnConfiguring(DbContextOptionsBuilder options)
//    {
//        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
//        var collectionPath = Path.Combine(appDataPath, "Anki2", "Yura", "collection.anki2");

//        var connectionString = $"Data Source={collectionPath};Mode=ReadOnly;";
//        options.UseSqlite(connectionString);
//    }

//}
