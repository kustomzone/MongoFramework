﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoFramework.Attributes;
using MongoFramework.Infrastructure;
using MongoFramework.Infrastructure.Linq;
using MongoFramework.Infrastructure.Mapping;
using MongoFramework.Linq;
using System;
using System.Linq;

namespace MongoFramework.Tests.Linq
{
	[TestClass]
	public class LinqExtensionsTests : TestBase
	{
		public class LinqExtensionsModel
		{
			public string Id { get; set; }
		}
		public class WhereIdMatchesGuidModel
		{
			public Guid Id { get; set; }
			public string Description { get; set; }
		}
		public class WhereIdMatchesObjectIdModel
		{
			public ObjectId Id { get; set; }
			public string Description { get; set; }
		}
		public class WhereIdMatchesStringModel
		{
			public string Id { get; set; }
			public string Description { get; set; }
		}

		public class SearchTextModel
		{
			public string Id { get; set; }
			[Index(IndexType.Text)]
			public string Text { get; set; }
			public int MiscField { get; set; }
		}

		[TestMethod]
		public void ValidToQuery()
		{
			EntityMapping.RegisterType(typeof(LinqExtensionsModel));

			var connection = TestConfiguration.GetConnection();
			var collection = connection.GetDatabase().GetCollection<LinqExtensionsModel>(nameof(LinqExtensionsModel));
			var underlyingQueryable = collection.AsQueryable();
			var queryable = new MongoFrameworkQueryable<LinqExtensionsModel, LinqExtensionsModel>(connection, underlyingQueryable);
			var result = LinqExtensions.ToQuery(queryable);

			Assert.AreEqual("db.LinqExtensionsModel.aggregate([])", result);
		}

		[TestMethod]
		[ExpectedException(typeof(ArgumentException), "ArgumentException")]
		public void InvalidToQuery()
		{
			LinqExtensions.ToQuery(null);
		}

		[TestMethod]
		public void WhereIdMatchesGuids()
		{
			var connection = TestConfiguration.GetConnection();
			var writerPipeline = new EntityWriterPipeline<WhereIdMatchesGuidModel>(TestConfiguration.GetConnection());
			var entityCollection = new EntityCollection<WhereIdMatchesGuidModel>()
			{
				new WhereIdMatchesGuidModel { Description = "1" },
				new WhereIdMatchesGuidModel { Description = "2" },
				new WhereIdMatchesGuidModel { Description = "3" },
				new WhereIdMatchesGuidModel { Description = "4" }
			};
			writerPipeline.AddCollection(entityCollection);
			writerPipeline.Write();

			var collection = TestConfiguration.GetConnection().GetDatabase().GetCollection<WhereIdMatchesGuidModel>(nameof(WhereIdMatchesGuidModel));
			var underlyingQueryable = collection.AsQueryable();
			var queryable = new MongoFrameworkQueryable<WhereIdMatchesGuidModel, WhereIdMatchesGuidModel>(connection, underlyingQueryable);

			var entityIds = entityCollection.Select(e => e.Id).Take(2);

			var idMatchQueryable = LinqExtensions.WhereIdMatches(queryable, entityIds);

			Assert.AreEqual(2, idMatchQueryable.Count());
			Assert.IsTrue(idMatchQueryable.ToList().All(e => entityIds.Contains(e.Id)));
		}

		[TestMethod]
		public void WhereIdMatchesObjectIds()
		{
			var connection = TestConfiguration.GetConnection();
			var writerPipeline = new EntityWriterPipeline<WhereIdMatchesObjectIdModel>(connection);
			var entityCollection = new EntityCollection<WhereIdMatchesObjectIdModel>()
			{
				new WhereIdMatchesObjectIdModel { Description = "1" },
				new WhereIdMatchesObjectIdModel { Description = "2" },
				new WhereIdMatchesObjectIdModel { Description = "3" },
				new WhereIdMatchesObjectIdModel { Description = "4" }
			};
			writerPipeline.AddCollection(entityCollection);
			writerPipeline.Write();

			var collection = connection.GetDatabase().GetCollection<WhereIdMatchesObjectIdModel>(nameof(WhereIdMatchesObjectIdModel));
			var underlyingQueryable = collection.AsQueryable();
			var queryable = new MongoFrameworkQueryable<WhereIdMatchesObjectIdModel, WhereIdMatchesObjectIdModel>(connection, underlyingQueryable);

			var entityIds = entityCollection.Select(e => e.Id).Take(2);

			var idMatchQueryable = LinqExtensions.WhereIdMatches(queryable, entityIds);

			Assert.AreEqual(2, idMatchQueryable.Count());
			Assert.IsTrue(idMatchQueryable.ToList().All(e => entityIds.Contains(e.Id)));
		}

		[TestMethod]
		public void WhereIdMatchesStringIds()
		{
			var connection = TestConfiguration.GetConnection();
			var writerPipeline = new EntityWriterPipeline<WhereIdMatchesStringModel>(connection);
			var entityCollection = new EntityCollection<WhereIdMatchesStringModel>()
			{
				new WhereIdMatchesStringModel { Description = "1" },
				new WhereIdMatchesStringModel { Description = "2" },
				new WhereIdMatchesStringModel { Description = "3" },
				new WhereIdMatchesStringModel { Description = "4" }
			};
			writerPipeline.AddCollection(entityCollection);
			writerPipeline.Write();

			var collection = connection.GetDatabase().GetCollection<WhereIdMatchesStringModel>(nameof(WhereIdMatchesStringModel));
			var underlyingQueryable = collection.AsQueryable();
			var queryable = new MongoFrameworkQueryable<WhereIdMatchesStringModel, WhereIdMatchesStringModel>(connection, underlyingQueryable);

			var entityIds = entityCollection.Select(e => e.Id).Take(2);

			var idMatchQueryable = LinqExtensions.WhereIdMatches(queryable, entityIds);

			Assert.AreEqual(2, idMatchQueryable.Count());
			Assert.IsTrue(idMatchQueryable.ToList().All(e => entityIds.Contains(e.Id)));
		}

		[TestMethod]
		public void SearchText()
		{
			var connection = TestConfiguration.GetConnection();
			var dbSet = new MongoDbSet<SearchTextModel>();
			dbSet.SetConnection(connection);

			dbSet.AddRange(new SearchTextModel[]
			{
				new SearchTextModel { MiscField = 1, Text = "The quick brown fox jumps over the lazy dog." },
				new SearchTextModel { MiscField = 2, Text = "The five boxing wizards jump quickly." },
				new SearchTextModel { MiscField = 3, Text = "The quick brown fox jumps over the lazy dog." },
				new SearchTextModel { MiscField = 4, Text = "Jived fox nymph grabs quick waltz." },
			});
			dbSet.SaveChanges();

			Assert.AreEqual(4, dbSet.SearchText("quick").Count());
			Assert.AreEqual(0, dbSet.SearchText("the").Count()); //Stop words aren't used in text indexes: https://docs.mongodb.com/manual/core/index-text/#supported-languages-and-stop-words
			Assert.AreEqual(2, dbSet.SearchText("dog").Count());
			Assert.AreEqual(1, dbSet.SearchText("jived").Count());

			//The order of commands matter for building the pipeline
			Assert.ThrowsException<MongoCommandException>(() => dbSet.Where(e => e.MiscField == 1).SearchText("quick").Count());
			Assert.AreEqual(1, dbSet.SearchText("quick").Where(e => e.MiscField == 3).Count());
		}
	}
}
