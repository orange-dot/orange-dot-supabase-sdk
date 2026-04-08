using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using OrangeDot.Supabase.Internal;
using Supabase.Core.Interfaces;
using Supabase.Realtime.Broadcast;
using Supabase.Realtime.Channel;
using Supabase.Realtime.Exceptions;
using Supabase.Realtime.Models;
using Supabase.Realtime.Presence;
using Supabase.Realtime.PostgresChanges;
using Supabase.Realtime.Socket;
using Supabase.Realtime;
using Xunit;

namespace OrangeDot.Supabase.Tests.Table;

public sealed class SupabaseTableTests
{
    [Fact]
    public void GetHeaders_getter_reads_through_and_setter_is_blocked_across_supported_references()
    {
        var inner = new FakePostgrestTable();
        Func<Dictionary<string, string>> initialHeaders = StaticHeaders;
        Func<Dictionary<string, string>> alternateHeaders = AlternateHeaders;
        inner.GetHeaders = initialHeaders;

        var wrapper = new SupabaseTable<TableModel>(inner, new FakeRealtimeClient());
        var tableInterface = (ISupabaseTable<TableModel>)wrapper;
        var postgrestInterface = (global::Supabase.Postgrest.Interfaces.IPostgrestTable<TableModel>)wrapper;
        var gettableHeaders = (IGettableHeaders)wrapper;

        Assert.Same(initialHeaders, wrapper.GetHeaders);
        Assert.Same(initialHeaders, tableInterface.GetHeaders);
        Assert.Same(initialHeaders, postgrestInterface.GetHeaders);
        Assert.Same(initialHeaders, gettableHeaders.GetHeaders);

        var concreteException = Assert.Throws<NotSupportedException>(() => wrapper.GetHeaders = alternateHeaders);
        Assert.Contains("does not support external GetHeaders assignment", concreteException.Message);
        Assert.Same(initialHeaders, inner.GetHeaders);

        Assert.Throws<NotSupportedException>(() => tableInterface.GetHeaders = alternateHeaders);
        Assert.Same(initialHeaders, inner.GetHeaders);

        Assert.Throws<NotSupportedException>(() => postgrestInterface.GetHeaders = alternateHeaders);
        Assert.Same(initialHeaders, inner.GetHeaders);

        Assert.Throws<NotSupportedException>(() => gettableHeaders.GetHeaders = alternateHeaders);
        Assert.Same(initialHeaders, inner.GetHeaders);
    }

    [Fact]
    public void Wrapper_delegates_fluent_members_and_preserves_wrapper_instance()
    {
        var inner = new FakePostgrestTable();
        var wrapper = new SupabaseTable<TableModel>(inner, new FakeRealtimeClient());
        var interfaceWrapper = (ISupabaseTable<TableModel>)wrapper;
        var filter = new FakeQueryFilter("id", global::Supabase.Postgrest.Constants.Operator.Equals, 1);
        Assert.Same(wrapper, wrapper.And(new List<global::Supabase.Postgrest.Interfaces.IPostgrestQueryFilter> { filter }));
        Assert.Same(wrapper, wrapper.Columns(new[] { "id", "name" }));
        Assert.Same(wrapper, wrapper.Columns(model => new object[] { model.Name!, model.Id }));
        Assert.Same(wrapper, wrapper.Filter("name", global::Supabase.Postgrest.Constants.Operator.Equals, "todo"));
        Assert.Same(wrapper, wrapper.Filter(model => model.Name!, global::Supabase.Postgrest.Constants.Operator.Equals, "todo"));
        Assert.Same(wrapper, wrapper.Limit(5, "projects"));
        Assert.Same(wrapper, wrapper.Match(new Dictionary<string, string> { ["name"] = "todo" }));
        Assert.Same(wrapper, wrapper.Match(new TableModel { Id = 7, Name = "todo" }));
        Assert.Same(wrapper, wrapper.Not(filter));
        Assert.Same(wrapper, wrapper.Not("metadata", global::Supabase.Postgrest.Constants.Operator.Contains, new Dictionary<string, object> { ["tag"] = "x" }));
        Assert.Same(wrapper, wrapper.Not(model => model.Metadata!, global::Supabase.Postgrest.Constants.Operator.Contains, new Dictionary<string, object> { ["tag"] = "x" }));
        Assert.Same(wrapper, wrapper.Not("name", global::Supabase.Postgrest.Constants.Operator.In, new List<string> { "a", "b" }));
        Assert.Same(wrapper, wrapper.Not(model => model.Name!, global::Supabase.Postgrest.Constants.Operator.In, new List<string> { "a", "b" }));
        Assert.Same(wrapper, wrapper.Not("name", global::Supabase.Postgrest.Constants.Operator.Equals, "todo"));
        Assert.Same(wrapper, wrapper.Not(model => model.Name!, global::Supabase.Postgrest.Constants.Operator.Equals, "todo"));
        Assert.Same(wrapper, wrapper.Offset(3, "projects"));
        Assert.Same(wrapper, wrapper.OnConflict("id"));
        Assert.Same(wrapper, wrapper.OnConflict(model => model.Id));
        Assert.Same(wrapper, wrapper.Or(new List<global::Supabase.Postgrest.Interfaces.IPostgrestQueryFilter> { filter }));
        Assert.Same(wrapper, wrapper.Order("id", global::Supabase.Postgrest.Constants.Ordering.Descending, global::Supabase.Postgrest.Constants.NullPosition.Last));
        Assert.Same(wrapper, wrapper.Order(model => model.Id, global::Supabase.Postgrest.Constants.Ordering.Ascending));
        Assert.Same(wrapper, wrapper.Order("projects", "id", global::Supabase.Postgrest.Constants.Ordering.Descending));
        Assert.Same(wrapper, wrapper.Range(2));
        Assert.Same(wrapper, wrapper.Range(2, 4));
        Assert.Same(wrapper, wrapper.Select("*"));
        Assert.Same(wrapper, wrapper.Select(model => new object[] { model.Id, model.Name! }));
        Assert.Same(wrapper, wrapper.Set(model => model.Name!, "renamed"));
        Assert.Same(wrapper, wrapper.Set(model => new KeyValuePair<object, object?>(model.Name!, "renamed")));
        Assert.Same(wrapper, wrapper.Where(model => model.Id == 7));
        Assert.Same(wrapper, interfaceWrapper.Where(model => model.Id == 8));

        wrapper.Clear();

        Assert.Equal(wrapper.BaseUrl, inner.BaseUrl);
        Assert.Equal(wrapper.TableName, inner.TableName);
        Assert.Equal(wrapper.GenerateUrl(), inner.GenerateUrl());
        Assert.Contains("And", inner.Calls);
        Assert.Contains("Columns(string[])", inner.Calls);
        Assert.Contains("Columns(expr)", inner.Calls);
        Assert.Contains("Filter(string)", inner.Calls);
        Assert.Contains("Filter(expr)", inner.Calls);
        Assert.Contains("Limit", inner.Calls);
        Assert.Contains("Match(dict)", inner.Calls);
        Assert.Contains("Match(model)", inner.Calls);
        Assert.Contains("Not(filter)", inner.Calls);
        Assert.Contains("Not(string,dict)", inner.Calls);
        Assert.Contains("Not(expr,dict)", inner.Calls);
        Assert.Contains("Not(string,list)", inner.Calls);
        Assert.Contains("Not(expr,list)", inner.Calls);
        Assert.Contains("Not(string,value)", inner.Calls);
        Assert.Contains("Not(expr,value)", inner.Calls);
        Assert.Contains("Offset", inner.Calls);
        Assert.Contains("OnConflict(string)", inner.Calls);
        Assert.Contains("OnConflict(expr)", inner.Calls);
        Assert.Contains("Or", inner.Calls);
        Assert.Contains("Order(string)", inner.Calls);
        Assert.Contains("Order(expr)", inner.Calls);
        Assert.Contains("Order(foreign)", inner.Calls);
        Assert.Contains("Range(from)", inner.Calls);
        Assert.Contains("Range(from,to)", inner.Calls);
        Assert.Contains("Select(string)", inner.Calls);
        Assert.Contains("Select(expr)", inner.Calls);
        Assert.Contains("Set(value)", inner.Calls);
        Assert.Contains("Set(kvp)", inner.Calls);
        Assert.Contains("Where", inner.Calls);
        Assert.Contains("Clear", inner.Calls);
    }

    [Fact]
    public void Wrapper_delegates_execution_members_including_upsert()
    {
        var inner = new FakePostgrestTable();
        var wrapper = new SupabaseTable<TableModel>(inner, new FakeRealtimeClient());
        var model = new TableModel { Id = 7, Name = "todo" };
        var models = new List<TableModel> { model };

        Assert.Same(inner.CountTask, wrapper.Count(global::Supabase.Postgrest.Constants.CountType.Exact));
        Assert.Same(inner.DeleteTask, wrapper.Delete());
        Assert.Same(inner.DeleteModelTask, wrapper.Delete(model));
        Assert.Same(inner.GetTask, wrapper.Get());
        Assert.Same(inner.InsertManyTask, wrapper.Insert(models));
        Assert.Same(inner.InsertSingleTask, wrapper.Insert(model));
        Assert.Same(inner.SingleTask, wrapper.Single());
        Assert.Same(inner.UpdateTask, wrapper.Update());
        Assert.Same(inner.UpdateModelTask, wrapper.Update(model));
        Assert.Same(inner.UpsertManyTask, wrapper.Upsert(models));
        Assert.Same(inner.UpsertSingleTask, wrapper.Upsert(model));

        Assert.Contains("Count", inner.Calls);
        Assert.Contains("Delete()", inner.Calls);
        Assert.Contains("Delete(model)", inner.Calls);
        Assert.Contains("Get", inner.Calls);
        Assert.Contains("Insert(many)", inner.Calls);
        Assert.Contains("Insert(single)", inner.Calls);
        Assert.Contains("Single", inner.Calls);
        Assert.Contains("Update()", inner.Calls);
        Assert.Contains("Update(model)", inner.Calls);
        Assert.Contains("Upsert(many)", inner.Calls);
        Assert.Contains("Upsert(single)", inner.Calls);
    }

    [Fact]
    public async Task On_auto_connects_when_socket_is_missing_and_registers_before_subscribe()
    {
        var realtime = new FakeRealtimeClient { HasSocket = false };
        var wrapper = new SupabaseTable<TableModel>(new FakePostgrestTable(), realtime);
        var parameters = new Dictionary<string, string> { ["tenant"] = "acme" };

        var channel = await wrapper.On(
            PostgresChangesOptions.ListenType.Inserts,
            static (_, _) => { },
            schema: "custom",
            filter: "id=eq.7",
            parameters: parameters,
            timeoutMs: 1234);

        var fakeChannel = Assert.IsType<FakeRealtimeChannel>(channel);

        Assert.Equal(1, realtime.ConnectAsyncCallCount);
        Assert.Single(realtime.ChannelNames);
        Assert.Equal(32, realtime.ChannelNames[0].Length);
        Assert.Equal(new[] { "Register", "AddPostgresChangeHandler", "Subscribe" }, fakeChannel.CallOrder);
        Assert.Equal("todos", fakeChannel.RegisteredOptions!.Table);
        Assert.Equal("custom", fakeChannel.RegisteredOptions.Schema);
        Assert.Equal("id=eq.7", fakeChannel.RegisteredOptions.Filter);
        Assert.Same(parameters, fakeChannel.RegisteredOptions.Parameters);
        Assert.Equal(PostgresChangesOptions.ListenType.Inserts, fakeChannel.RegisteredListenType);
        Assert.Equal(1234, fakeChannel.SubscribeTimeoutMs);
    }

    [Fact]
    public async Task On_skips_connect_when_socket_is_already_available_and_uses_fresh_channel_per_call()
    {
        var realtime = new FakeRealtimeClient { HasSocket = true };
        var wrapper = new SupabaseTable<TableModel>(new FakePostgrestTable(), realtime);

        var first = await wrapper.On(PostgresChangesOptions.ListenType.Inserts, static (_, _) => { });
        var second = await wrapper.On(PostgresChangesOptions.ListenType.Updates, static (_, _) => { });

        Assert.Equal(0, realtime.ConnectAsyncCallCount);
        Assert.Equal(2, realtime.ChannelNames.Count);
        Assert.NotEqual(realtime.ChannelNames[0], realtime.ChannelNames[1]);
        Assert.NotSame(first, second);
    }

    [Fact]
    public async Task On_removes_channel_when_subscribe_throws()
    {
        var realtime = new FakeRealtimeClient { HasSocket = true, ThrowOnSubscribe = true };
        var wrapper = new SupabaseTable<TableModel>(new FakePostgrestTable(), realtime);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            wrapper.On(PostgresChangesOptions.ListenType.Inserts, static (_, _) => { }));

        Assert.Single(realtime.RemovedTopics);
    }

    [Fact]
    public void Shell_blocks_table_access_before_readiness_and_forwards_after_readiness()
    {
        var shell = new SupabaseClientShell(new NullLogger<SupabaseClientShell>());

        Assert.Throws<InvalidOperationException>(() => shell.Table<TableModel>());

        var readyClient = CreateReadyClient();
        shell.SetInitializedClient(readyClient);

        var table = shell.Table<TableModel>();

        Assert.IsAssignableFrom<ISupabaseTable<TableModel>>(table);
    }

    private static SupabaseClient CreateReadyClient()
    {
        var configured = SupabaseClient.Configure(new SupabaseOptions
        {
            Url = "https://abc.supabase.co",
            AnonKey = "anon-key"
        });

        var hydrated = configured.LoadPersistedSessionAsync().GetAwaiter().GetResult();
        return hydrated.InitializeAsync().GetAwaiter().GetResult();
    }

    private static Dictionary<string, string> StaticHeaders() => new()
    {
        ["apikey"] = "anon-key"
    };

    private static Dictionary<string, string> AlternateHeaders() => new()
    {
        ["apikey"] = "service-role-key"
    };

    public sealed class TableModel : global::Supabase.Postgrest.Models.BaseModel
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public Dictionary<string, object>? Metadata { get; set; }
    }

    private sealed class FakeQueryFilter : global::Supabase.Postgrest.Interfaces.IPostgrestQueryFilter
    {
        public FakeQueryFilter(string? property, global::Supabase.Postgrest.Constants.Operator op, object? criteria)
        {
            Property = property;
            Op = op;
            Criteria = criteria;
        }

        public object? Criteria { get; }

        public global::Supabase.Postgrest.Constants.Operator Op { get; }

        public string? Property { get; }
    }

    private sealed class FakePostgrestTable : global::Supabase.Postgrest.Interfaces.IPostgrestTable<TableModel>
    {
        public List<string> Calls { get; } = new();

        public string BaseUrl { get; } = "https://example.supabase.co/rest/v1";

        public string TableName { get; } = "todos";

        public Func<Dictionary<string, string>>? GetHeaders { get; set; }

        public Task<int> CountTask { get; } = Task.FromResult(42);

        public Task DeleteTask { get; } = Task.CompletedTask;

        public Task<global::Supabase.Postgrest.Responses.ModeledResponse<TableModel>> DeleteModelTask { get; } =
            Task.FromResult<global::Supabase.Postgrest.Responses.ModeledResponse<TableModel>>(null!);

        public Task<global::Supabase.Postgrest.Responses.ModeledResponse<TableModel>> GetTask { get; } =
            Task.FromResult<global::Supabase.Postgrest.Responses.ModeledResponse<TableModel>>(null!);

        public Task<global::Supabase.Postgrest.Responses.ModeledResponse<TableModel>> InsertManyTask { get; } =
            Task.FromResult<global::Supabase.Postgrest.Responses.ModeledResponse<TableModel>>(null!);

        public Task<global::Supabase.Postgrest.Responses.ModeledResponse<TableModel>> InsertSingleTask { get; } =
            Task.FromResult<global::Supabase.Postgrest.Responses.ModeledResponse<TableModel>>(null!);

        public Task<TableModel?> SingleTask { get; } = Task.FromResult<TableModel?>(new TableModel { Id = 7 });

        public Task<global::Supabase.Postgrest.Responses.ModeledResponse<TableModel>> UpdateTask { get; } =
            Task.FromResult<global::Supabase.Postgrest.Responses.ModeledResponse<TableModel>>(null!);

        public Task<global::Supabase.Postgrest.Responses.ModeledResponse<TableModel>> UpdateModelTask { get; } =
            Task.FromResult<global::Supabase.Postgrest.Responses.ModeledResponse<TableModel>>(null!);

        public Task<global::Supabase.Postgrest.Responses.ModeledResponse<TableModel>> UpsertManyTask { get; } =
            Task.FromResult<global::Supabase.Postgrest.Responses.ModeledResponse<TableModel>>(null!);

        public Task<global::Supabase.Postgrest.Responses.ModeledResponse<TableModel>> UpsertSingleTask { get; } =
            Task.FromResult<global::Supabase.Postgrest.Responses.ModeledResponse<TableModel>>(null!);

        public string GenerateUrl() => $"{BaseUrl}/{TableName}";

        public global::Supabase.Postgrest.Interfaces.IPostgrestTable<TableModel> And(List<global::Supabase.Postgrest.Interfaces.IPostgrestQueryFilter> filters)
        {
            Calls.Add("And");
            return this;
        }

        public void Clear() => Calls.Add("Clear");

        public global::Supabase.Postgrest.Interfaces.IPostgrestTable<TableModel> Columns(string[] columns)
        {
            Calls.Add("Columns(string[])");
            return this;
        }

        public global::Supabase.Postgrest.Interfaces.IPostgrestTable<TableModel> Columns(Expression<Func<TableModel, object[]>> predicate)
        {
            Calls.Add("Columns(expr)");
            return this;
        }

        public Task<int> Count(global::Supabase.Postgrest.Constants.CountType type, CancellationToken cancellationToken = default)
        {
            Calls.Add("Count");
            return CountTask;
        }

        public Task Delete(global::Supabase.Postgrest.QueryOptions? options = null, CancellationToken cancellationToken = default)
        {
            Calls.Add("Delete()");
            return DeleteTask;
        }

        public Task<global::Supabase.Postgrest.Responses.ModeledResponse<TableModel>> Delete(TableModel model, global::Supabase.Postgrest.QueryOptions? options = null, CancellationToken cancellationToken = default)
        {
            Calls.Add("Delete(model)");
            return DeleteModelTask;
        }

        public global::Supabase.Postgrest.Interfaces.IPostgrestTable<TableModel> Filter<TCriterion>(string columnName, global::Supabase.Postgrest.Constants.Operator op, TCriterion? criterion)
        {
            Calls.Add("Filter(string)");
            return this;
        }

        public global::Supabase.Postgrest.Interfaces.IPostgrestTable<TableModel> Filter<TCriterion>(Expression<Func<TableModel, object>> predicate, global::Supabase.Postgrest.Constants.Operator op, TCriterion? criterion)
        {
            Calls.Add("Filter(expr)");
            return this;
        }

        public Task<global::Supabase.Postgrest.Responses.ModeledResponse<TableModel>> Get(CancellationToken cancellationToken = default, global::Supabase.Postgrest.Constants.CountType countType = global::Supabase.Postgrest.Constants.CountType.Estimated)
        {
            Calls.Add("Get");
            return GetTask;
        }

        public Task<global::Supabase.Postgrest.Responses.ModeledResponse<TableModel>> Insert(ICollection<TableModel> models, global::Supabase.Postgrest.QueryOptions? options = null, CancellationToken cancellationToken = default)
        {
            Calls.Add("Insert(many)");
            return InsertManyTask;
        }

        public Task<global::Supabase.Postgrest.Responses.ModeledResponse<TableModel>> Insert(TableModel model, global::Supabase.Postgrest.QueryOptions? options = null, CancellationToken cancellationToken = default)
        {
            Calls.Add("Insert(single)");
            return InsertSingleTask;
        }

        public global::Supabase.Postgrest.Interfaces.IPostgrestTable<TableModel> Limit(int limit, string? foreignTableName = null)
        {
            Calls.Add("Limit");
            return this;
        }

        public global::Supabase.Postgrest.Interfaces.IPostgrestTable<TableModel> Match(Dictionary<string, string> query)
        {
            Calls.Add("Match(dict)");
            return this;
        }

        public global::Supabase.Postgrest.Interfaces.IPostgrestTable<TableModel> Match(TableModel model)
        {
            Calls.Add("Match(model)");
            return this;
        }

        public global::Supabase.Postgrest.Interfaces.IPostgrestTable<TableModel> Not(global::Supabase.Postgrest.Interfaces.IPostgrestQueryFilter filter)
        {
            Calls.Add("Not(filter)");
            return this;
        }

        public global::Supabase.Postgrest.Interfaces.IPostgrestTable<TableModel> Not(string columnName, global::Supabase.Postgrest.Constants.Operator op, Dictionary<string, object> criteria)
        {
            Calls.Add("Not(string,dict)");
            return this;
        }

        public global::Supabase.Postgrest.Interfaces.IPostgrestTable<TableModel> Not(Expression<Func<TableModel, object>> predicate, global::Supabase.Postgrest.Constants.Operator op, Dictionary<string, object> criteria)
        {
            Calls.Add("Not(expr,dict)");
            return this;
        }

        public global::Supabase.Postgrest.Interfaces.IPostgrestTable<TableModel> Not<TCriterion>(string columnName, global::Supabase.Postgrest.Constants.Operator op, List<TCriterion> criteria)
        {
            Calls.Add("Not(string,list)");
            return this;
        }

        public global::Supabase.Postgrest.Interfaces.IPostgrestTable<TableModel> Not<TCriterion>(Expression<Func<TableModel, object>> predicate, global::Supabase.Postgrest.Constants.Operator op, List<TCriterion> criteria)
        {
            Calls.Add("Not(expr,list)");
            return this;
        }

        public global::Supabase.Postgrest.Interfaces.IPostgrestTable<TableModel> Not<TCriterion>(string columnName, global::Supabase.Postgrest.Constants.Operator op, TCriterion? criterion)
        {
            Calls.Add("Not(string,value)");
            return this;
        }

        public global::Supabase.Postgrest.Interfaces.IPostgrestTable<TableModel> Not<TCriterion>(Expression<Func<TableModel, object>> predicate, global::Supabase.Postgrest.Constants.Operator op, TCriterion? criterion)
        {
            Calls.Add("Not(expr,value)");
            return this;
        }

        public global::Supabase.Postgrest.Interfaces.IPostgrestTable<TableModel> Offset(int offset, string? foreignTableName = null)
        {
            Calls.Add("Offset");
            return this;
        }

        public global::Supabase.Postgrest.Interfaces.IPostgrestTable<TableModel> OnConflict(string columnName)
        {
            Calls.Add("OnConflict(string)");
            return this;
        }

        public global::Supabase.Postgrest.Interfaces.IPostgrestTable<TableModel> OnConflict(Expression<Func<TableModel, object>> predicate)
        {
            Calls.Add("OnConflict(expr)");
            return this;
        }

        public global::Supabase.Postgrest.Interfaces.IPostgrestTable<TableModel> Or(List<global::Supabase.Postgrest.Interfaces.IPostgrestQueryFilter> filters)
        {
            Calls.Add("Or");
            return this;
        }

        public global::Supabase.Postgrest.Interfaces.IPostgrestTable<TableModel> Order(string column, global::Supabase.Postgrest.Constants.Ordering ordering, global::Supabase.Postgrest.Constants.NullPosition nullPosition = global::Supabase.Postgrest.Constants.NullPosition.First)
        {
            Calls.Add("Order(string)");
            return this;
        }

        public global::Supabase.Postgrest.Interfaces.IPostgrestTable<TableModel> Order(Expression<Func<TableModel, object>> predicate, global::Supabase.Postgrest.Constants.Ordering ordering, global::Supabase.Postgrest.Constants.NullPosition nullPosition = global::Supabase.Postgrest.Constants.NullPosition.First)
        {
            Calls.Add("Order(expr)");
            return this;
        }

        public global::Supabase.Postgrest.Interfaces.IPostgrestTable<TableModel> Order(string foreignTable, string column, global::Supabase.Postgrest.Constants.Ordering ordering, global::Supabase.Postgrest.Constants.NullPosition nullPosition = global::Supabase.Postgrest.Constants.NullPosition.First)
        {
            Calls.Add("Order(foreign)");
            return this;
        }

        public global::Supabase.Postgrest.Interfaces.IPostgrestTable<TableModel> Range(int from)
        {
            Calls.Add("Range(from)");
            return this;
        }

        public global::Supabase.Postgrest.Interfaces.IPostgrestTable<TableModel> Range(int from, int to)
        {
            Calls.Add("Range(from,to)");
            return this;
        }

        public global::Supabase.Postgrest.Interfaces.IPostgrestTable<TableModel> Select(string columnQuery)
        {
            Calls.Add("Select(string)");
            return this;
        }

        public global::Supabase.Postgrest.Interfaces.IPostgrestTable<TableModel> Select(Expression<Func<TableModel, object[]>> predicate)
        {
            Calls.Add("Select(expr)");
            return this;
        }

        public Task<TableModel?> Single(CancellationToken cancellationToken = default)
        {
            Calls.Add("Single");
            return SingleTask;
        }

        public global::Supabase.Postgrest.Interfaces.IPostgrestTable<TableModel> Set(Expression<Func<TableModel, object>> keySelector, object? value)
        {
            Calls.Add("Set(value)");
            return this;
        }

        public global::Supabase.Postgrest.Interfaces.IPostgrestTable<TableModel> Set(Expression<Func<TableModel, KeyValuePair<object, object?>>> keyValuePairExpression)
        {
            Calls.Add("Set(kvp)");
            return this;
        }

        public Task<global::Supabase.Postgrest.Responses.ModeledResponse<TableModel>> Update(global::Supabase.Postgrest.QueryOptions? options = null, CancellationToken cancellationToken = default)
        {
            Calls.Add("Update()");
            return UpdateTask;
        }

        public Task<global::Supabase.Postgrest.Responses.ModeledResponse<TableModel>> Update(TableModel model, global::Supabase.Postgrest.QueryOptions? options = null, CancellationToken cancellationToken = default)
        {
            Calls.Add("Update(model)");
            return UpdateModelTask;
        }

        public Task<global::Supabase.Postgrest.Responses.ModeledResponse<TableModel>> Upsert(ICollection<TableModel> model, global::Supabase.Postgrest.QueryOptions? options = null, CancellationToken cancellationToken = default)
        {
            Calls.Add("Upsert(many)");
            return UpsertManyTask;
        }

        public Task<global::Supabase.Postgrest.Responses.ModeledResponse<TableModel>> Upsert(TableModel model, global::Supabase.Postgrest.QueryOptions? options = null, CancellationToken cancellationToken = default)
        {
            Calls.Add("Upsert(single)");
            return UpsertSingleTask;
        }

        public global::Supabase.Postgrest.Interfaces.IPostgrestTable<TableModel> Where(Expression<Func<TableModel, bool>> predicate)
        {
            Calls.Add("Where");
            return this;
        }
    }

    private sealed class FakeRealtimeClient : ISupabaseTableRealtimeClient
    {
        public bool HasSocket { get; set; }

        public bool ThrowOnSubscribe { get; set; }

        public int ConnectAsyncCallCount { get; private set; }

        public List<string> ChannelNames { get; } = new();

        public List<string> RemovedTopics { get; } = new();

        public Task ConnectAsync()
        {
            ConnectAsyncCallCount++;
            HasSocket = true;
            return Task.CompletedTask;
        }

        public global::Supabase.Realtime.Interfaces.IRealtimeChannel Channel(string channelName)
        {
            ChannelNames.Add(channelName);
            return new FakeRealtimeChannel(channelName, ThrowOnSubscribe);
        }

        public void Remove(global::Supabase.Realtime.Interfaces.IRealtimeChannel channel)
        {
            RemovedTopics.Add(channel.Topic);
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeRealtimeChannel : global::Supabase.Realtime.Interfaces.IRealtimeChannel
    {
        private readonly bool _throwOnSubscribe;

        public FakeRealtimeChannel(string topic, bool throwOnSubscribe)
        {
            Topic = topic;
            _throwOnSubscribe = throwOnSubscribe;
            Options = new ChannelOptions(new global::Supabase.Realtime.ClientOptions(), static () => null, new Newtonsoft.Json.JsonSerializerSettings());
        }

        public List<string> CallOrder { get; } = new();

        public PostgresChangesOptions? RegisteredOptions { get; private set; }

        public PostgresChangesOptions.ListenType? RegisteredListenType { get; private set; }

        public int SubscribeTimeoutMs { get; private set; }

        public bool HasJoinedOnce { get; private set; }

        public bool IsClosed => false;

        public bool IsErrored => false;

        public bool IsJoined => false;

        public bool IsJoining => false;

        public bool IsLeaving => false;

        public ChannelOptions Options { get; }

        public BroadcastOptions? BroadcastOptions => null;

        public PresenceOptions? PresenceOptions => null;

        public List<PostgresChangesOptions> PostgresChangesOptions { get; } = new();

        public global::Supabase.Realtime.Constants.ChannelState State => global::Supabase.Realtime.Constants.ChannelState.Closed;

        public string Topic { get; }

        public void AddStateChangedHandler(global::Supabase.Realtime.Interfaces.IRealtimeChannel.StateChangedHandler stateChangedHandler) { }

        public void RemoveStateChangedHandler(global::Supabase.Realtime.Interfaces.IRealtimeChannel.StateChangedHandler stateChangedHandler) { }

        public void ClearStateChangedHandlers() { }

        public void AddMessageReceivedHandler(global::Supabase.Realtime.Interfaces.IRealtimeChannel.MessageReceivedHandler messageReceivedHandler) { }

        public void RemoveMessageReceivedHandler(global::Supabase.Realtime.Interfaces.IRealtimeChannel.MessageReceivedHandler messageReceivedHandler) { }

        public void ClearMessageReceivedHandlers() { }

        public void AddPostgresChangeHandler(PostgresChangesOptions.ListenType listenType, global::Supabase.Realtime.Interfaces.IRealtimeChannel.PostgresChangesHandler postgresChangeHandler)
        {
            CallOrder.Add("AddPostgresChangeHandler");
            RegisteredListenType = listenType;
        }

        public void RemovePostgresChangeHandler(PostgresChangesOptions.ListenType listenType, global::Supabase.Realtime.Interfaces.IRealtimeChannel.PostgresChangesHandler postgresChangeHandler) { }

        public void ClearPostgresChangeHandlers() { }

        public void AddErrorHandler(global::Supabase.Realtime.Interfaces.IRealtimeChannel.ErrorEventHandler handler) { }

        public void RemoveErrorHandler(global::Supabase.Realtime.Interfaces.IRealtimeChannel.ErrorEventHandler handler) { }

        public void ClearErrorHandlers() { }

        public global::Supabase.Realtime.Interfaces.IRealtimeBroadcast? Broadcast() => null;

        public global::Supabase.Realtime.Interfaces.IRealtimePresence? Presence() => null;

        public Push Push(string eventName, string? type = null, object? payload = null, int timeoutMs = global::Supabase.Realtime.Constants.DefaultTimeout)
            => throw new NotSupportedException();

        public void Rejoin(int timeoutMs = global::Supabase.Realtime.Constants.DefaultTimeout) { }

        public Task<bool> Send(global::Supabase.Realtime.Constants.ChannelEventName eventType, string? type, object payload, int timeoutMs = global::Supabase.Realtime.Constants.DefaultTimeout)
            => throw new NotSupportedException();

        public global::Supabase.Realtime.RealtimeBroadcast<TBroadcastResponse> Register<TBroadcastResponse>(bool broadcastSelf = false, bool broadcastAck = false)
            where TBroadcastResponse : BaseBroadcast => throw new NotSupportedException();

        public global::Supabase.Realtime.RealtimePresence<TPresenceResponse> Register<TPresenceResponse>(string presenceKey)
            where TPresenceResponse : BasePresence => throw new NotSupportedException();

        public global::Supabase.Realtime.Interfaces.IRealtimeChannel Register(PostgresChangesOptions postgresChangesOptions)
        {
            CallOrder.Add("Register");
            RegisteredOptions = postgresChangesOptions;
            PostgresChangesOptions.Add(postgresChangesOptions);
            return this;
        }

        public Task<global::Supabase.Realtime.Interfaces.IRealtimeChannel> Subscribe(int timeoutMs = global::Supabase.Realtime.Constants.DefaultTimeout)
        {
            CallOrder.Add("Subscribe");
            SubscribeTimeoutMs = timeoutMs;
            HasJoinedOnce = true;

            if (_throwOnSubscribe)
            {
                throw new InvalidOperationException("subscribe failed");
            }

            return Task.FromResult<global::Supabase.Realtime.Interfaces.IRealtimeChannel>(this);
        }

        public global::Supabase.Realtime.Interfaces.IRealtimeChannel Unsubscribe() => this;
    }
}
