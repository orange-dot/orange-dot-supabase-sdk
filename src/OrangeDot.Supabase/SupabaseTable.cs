using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using OrangeDot.Supabase.Internal;

namespace OrangeDot.Supabase;

public sealed class SupabaseTable<TModel> : ISupabaseTable<TModel>
    where TModel : global::Supabase.Postgrest.Models.BaseModel, new()
{
    private readonly global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel> _inner;
    private readonly ISupabaseTableRealtimeClient _realtime;

    internal SupabaseTable(
        global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel> inner,
        ISupabaseTableRealtimeClient realtime)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(realtime);

        _inner = inner;
        _realtime = realtime;
    }

    public string BaseUrl => _inner.BaseUrl;

    public string TableName => _inner.TableName;

    public Func<Dictionary<string, string>>? GetHeaders
    {
        get => _inner.GetHeaders;
        set => _inner.GetHeaders = value;
    }

    public string GenerateUrl() => _inner.GenerateUrl();

    public ISupabaseTable<TModel> And(List<global::Supabase.Postgrest.Interfaces.IPostgrestQueryFilter> filters)
    {
        _inner.And(filters);
        return this;
    }

    public void Clear() => _inner.Clear();

    public ISupabaseTable<TModel> Columns(string[] columns)
    {
        _inner.Columns(columns);
        return this;
    }

    public ISupabaseTable<TModel> Columns(Expression<Func<TModel, object[]>> predicate)
    {
        _inner.Columns(predicate);
        return this;
    }

    public Task<int> Count(global::Supabase.Postgrest.Constants.CountType type, CancellationToken cancellationToken = default)
        => _inner.Count(type, cancellationToken);

    public Task Delete(global::Supabase.Postgrest.QueryOptions? options = null, CancellationToken cancellationToken = default)
        => _inner.Delete(options, cancellationToken);

    public Task<global::Supabase.Postgrest.Responses.ModeledResponse<TModel>> Delete(
        TModel model,
        global::Supabase.Postgrest.QueryOptions? options = null,
        CancellationToken cancellationToken = default)
        => _inner.Delete(model, options, cancellationToken);

    public ISupabaseTable<TModel> Filter<TCriterion>(
        string columnName,
        global::Supabase.Postgrest.Constants.Operator op,
        TCriterion criterion)
    {
        _inner.Filter(columnName, op, criterion);
        return this;
    }

    public ISupabaseTable<TModel> Filter<TCriterion>(
        Expression<Func<TModel, object>> predicate,
        global::Supabase.Postgrest.Constants.Operator op,
        TCriterion criterion)
    {
        _inner.Filter(predicate, op, criterion);
        return this;
    }

    public Task<global::Supabase.Postgrest.Responses.ModeledResponse<TModel>> Get(
        CancellationToken cancellationToken = default,
        global::Supabase.Postgrest.Constants.CountType countType = global::Supabase.Postgrest.Constants.CountType.Estimated)
        => _inner.Get(cancellationToken, countType);

    public Task<global::Supabase.Postgrest.Responses.ModeledResponse<TModel>> Insert(
        ICollection<TModel> models,
        global::Supabase.Postgrest.QueryOptions? options = null,
        CancellationToken cancellationToken = default)
        => _inner.Insert(models, options, cancellationToken);

    public Task<global::Supabase.Postgrest.Responses.ModeledResponse<TModel>> Insert(
        TModel model,
        global::Supabase.Postgrest.QueryOptions? options = null,
        CancellationToken cancellationToken = default)
        => _inner.Insert(model, options, cancellationToken);

    public ISupabaseTable<TModel> Limit(int limit, string? foreignTableName = null)
    {
        _inner.Limit(limit, foreignTableName);
        return this;
    }

    public ISupabaseTable<TModel> Match(Dictionary<string, string> query)
    {
        _inner.Match(query);
        return this;
    }

    public ISupabaseTable<TModel> Match(TModel model)
    {
        _inner.Match(model);
        return this;
    }

    public ISupabaseTable<TModel> Not(global::Supabase.Postgrest.Interfaces.IPostgrestQueryFilter filter)
    {
        _inner.Not(filter);
        return this;
    }

    public ISupabaseTable<TModel> Not(
        string columnName,
        global::Supabase.Postgrest.Constants.Operator op,
        Dictionary<string, object> criteria)
    {
        _inner.Not(columnName, op, criteria);
        return this;
    }

    public ISupabaseTable<TModel> Not(
        Expression<Func<TModel, object>> predicate,
        global::Supabase.Postgrest.Constants.Operator op,
        Dictionary<string, object> criteria)
    {
        _inner.Not(predicate, op, criteria);
        return this;
    }

    public ISupabaseTable<TModel> Not<TCriterion>(
        string columnName,
        global::Supabase.Postgrest.Constants.Operator op,
        List<TCriterion> criteria)
    {
        _inner.Not(columnName, op, criteria);
        return this;
    }

    public ISupabaseTable<TModel> Not<TCriterion>(
        Expression<Func<TModel, object>> predicate,
        global::Supabase.Postgrest.Constants.Operator op,
        List<TCriterion> criteria)
    {
        _inner.Not(predicate, op, criteria);
        return this;
    }

    public ISupabaseTable<TModel> Not<TCriterion>(
        string columnName,
        global::Supabase.Postgrest.Constants.Operator op,
        TCriterion criterion)
    {
        _inner.Not(columnName, op, criterion);
        return this;
    }

    public ISupabaseTable<TModel> Not<TCriterion>(
        Expression<Func<TModel, object>> predicate,
        global::Supabase.Postgrest.Constants.Operator op,
        TCriterion criterion)
    {
        _inner.Not(predicate, op, criterion);
        return this;
    }

    public ISupabaseTable<TModel> Offset(int offset, string? foreignTableName = null)
    {
        _inner.Offset(offset, foreignTableName);
        return this;
    }

    public ISupabaseTable<TModel> OnConflict(string columnName)
    {
        _inner.OnConflict(columnName);
        return this;
    }

    public ISupabaseTable<TModel> OnConflict(Expression<Func<TModel, object>> predicate)
    {
        _inner.OnConflict(predicate);
        return this;
    }

    public async Task<global::Supabase.Realtime.Interfaces.IRealtimeChannel> On(
        global::Supabase.Realtime.PostgresChanges.PostgresChangesOptions.ListenType listenType,
        global::Supabase.Realtime.Interfaces.IRealtimeChannel.PostgresChangesHandler handler,
        string schema = "public",
        string? filter = null,
        Dictionary<string, string>? parameters = null,
        int timeoutMs = global::Supabase.Realtime.Constants.DefaultTimeout)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);

        // ConnectAsync is expected to be idempotent; no lock needed
        // because each SupabaseTable instance is short-lived.
        if (!_realtime.HasSocket)
        {
            await _realtime.ConnectAsync();
        }

        var channel = _realtime.Channel(Guid.NewGuid().ToString("N"));
        var options = new global::Supabase.Realtime.PostgresChanges.PostgresChangesOptions(
            schema,
            _inner.TableName,
            listenType,
            filter,
            parameters);

        channel.Register(options);
        channel.AddPostgresChangeHandler(listenType, handler);

        return await channel.Subscribe(timeoutMs);
    }

    public ISupabaseTable<TModel> Or(List<global::Supabase.Postgrest.Interfaces.IPostgrestQueryFilter> filters)
    {
        _inner.Or(filters);
        return this;
    }

    public ISupabaseTable<TModel> Order(
        string column,
        global::Supabase.Postgrest.Constants.Ordering ordering,
        global::Supabase.Postgrest.Constants.NullPosition nullPosition = global::Supabase.Postgrest.Constants.NullPosition.First)
    {
        _inner.Order(column, ordering, nullPosition);
        return this;
    }

    public ISupabaseTable<TModel> Order(
        Expression<Func<TModel, object>> predicate,
        global::Supabase.Postgrest.Constants.Ordering ordering,
        global::Supabase.Postgrest.Constants.NullPosition nullPosition = global::Supabase.Postgrest.Constants.NullPosition.First)
    {
        _inner.Order(predicate, ordering, nullPosition);
        return this;
    }

    public ISupabaseTable<TModel> Order(
        string foreignTable,
        string column,
        global::Supabase.Postgrest.Constants.Ordering ordering,
        global::Supabase.Postgrest.Constants.NullPosition nullPosition = global::Supabase.Postgrest.Constants.NullPosition.First)
    {
        _inner.Order(foreignTable, column, ordering, nullPosition);
        return this;
    }

    public ISupabaseTable<TModel> Range(int from)
    {
        _inner.Range(from);
        return this;
    }

    public ISupabaseTable<TModel> Range(int from, int to)
    {
        _inner.Range(from, to);
        return this;
    }

    public ISupabaseTable<TModel> Select(string columnQuery)
    {
        _inner.Select(columnQuery);
        return this;
    }

    public ISupabaseTable<TModel> Select(Expression<Func<TModel, object[]>> predicate)
    {
        _inner.Select(predicate);
        return this;
    }

    public Task<TModel?> Single(CancellationToken cancellationToken = default)
        => _inner.Single(cancellationToken);

    public ISupabaseTable<TModel> Set(Expression<Func<TModel, object>> keySelector, object? value)
    {
        _inner.Set(keySelector, value);
        return this;
    }

    public ISupabaseTable<TModel> Set(Expression<Func<TModel, KeyValuePair<object, object?>>> keyValuePairExpression)
    {
        _inner.Set(keyValuePairExpression);
        return this;
    }

    public Task<global::Supabase.Postgrest.Responses.ModeledResponse<TModel>> Update(
        global::Supabase.Postgrest.QueryOptions? options = null,
        CancellationToken cancellationToken = default)
        => _inner.Update(options, cancellationToken);

    public Task<global::Supabase.Postgrest.Responses.ModeledResponse<TModel>> Update(
        TModel model,
        global::Supabase.Postgrest.QueryOptions? options = null,
        CancellationToken cancellationToken = default)
        => _inner.Update(model, options, cancellationToken);

    public Task<global::Supabase.Postgrest.Responses.ModeledResponse<TModel>> Upsert(
        ICollection<TModel> model,
        global::Supabase.Postgrest.QueryOptions? options = null,
        CancellationToken cancellationToken = default)
        => _inner.Upsert(model, options, cancellationToken);

    public Task<global::Supabase.Postgrest.Responses.ModeledResponse<TModel>> Upsert(
        TModel model,
        global::Supabase.Postgrest.QueryOptions? options = null,
        CancellationToken cancellationToken = default)
        => _inner.Upsert(model, options, cancellationToken);

    public ISupabaseTable<TModel> Where(Expression<Func<TModel, bool>> predicate)
    {
        _inner.Where(predicate);
        return this;
    }

    global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel> global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel>.And(List<global::Supabase.Postgrest.Interfaces.IPostgrestQueryFilter> filters) => And(filters);

    global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel> global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel>.Columns(string[] columns) => Columns(columns);

    global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel> global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel>.Columns(Expression<Func<TModel, object[]>> predicate) => Columns(predicate);

#pragma warning disable CS8769
    global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel> global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel>.Filter<TCriterion>(string columnName, global::Supabase.Postgrest.Constants.Operator op, TCriterion criterion) => Filter(columnName, op, criterion);

    global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel> global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel>.Filter<TCriterion>(Expression<Func<TModel, object>> predicate, global::Supabase.Postgrest.Constants.Operator op, TCriterion criterion) => Filter(predicate, op, criterion);

    global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel> global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel>.Limit(int limit, string? foreignTableName) => Limit(limit, foreignTableName);

    global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel> global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel>.Match(Dictionary<string, string> query) => Match(query);

    global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel> global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel>.Match(TModel model) => Match(model);

    global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel> global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel>.Not(global::Supabase.Postgrest.Interfaces.IPostgrestQueryFilter filter) => Not(filter);

    global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel> global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel>.Not(string columnName, global::Supabase.Postgrest.Constants.Operator op, Dictionary<string, object> criteria) => Not(columnName, op, criteria);

    global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel> global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel>.Not(Expression<Func<TModel, object>> predicate, global::Supabase.Postgrest.Constants.Operator op, Dictionary<string, object> criteria) => Not(predicate, op, criteria);

    global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel> global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel>.Not<TCriterion>(string columnName, global::Supabase.Postgrest.Constants.Operator op, List<TCriterion> criteria) => Not(columnName, op, criteria);

    global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel> global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel>.Not<TCriterion>(Expression<Func<TModel, object>> predicate, global::Supabase.Postgrest.Constants.Operator op, List<TCriterion> criteria) => Not(predicate, op, criteria);

    global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel> global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel>.Not<TCriterion>(string columnName, global::Supabase.Postgrest.Constants.Operator op, TCriterion criterion) => Not(columnName, op, criterion);

    global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel> global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel>.Not<TCriterion>(Expression<Func<TModel, object>> predicate, global::Supabase.Postgrest.Constants.Operator op, TCriterion criterion) => Not(predicate, op, criterion);
#pragma warning restore CS8769

    global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel> global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel>.Offset(int offset, string? foreignTableName) => Offset(offset, foreignTableName);

    global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel> global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel>.OnConflict(string columnName) => OnConflict(columnName);

    global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel> global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel>.OnConflict(Expression<Func<TModel, object>> predicate) => OnConflict(predicate);

    global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel> global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel>.Or(List<global::Supabase.Postgrest.Interfaces.IPostgrestQueryFilter> filters) => Or(filters);

    global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel> global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel>.Order(string column, global::Supabase.Postgrest.Constants.Ordering ordering, global::Supabase.Postgrest.Constants.NullPosition nullPosition) => Order(column, ordering, nullPosition);

    global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel> global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel>.Order(Expression<Func<TModel, object>> predicate, global::Supabase.Postgrest.Constants.Ordering ordering, global::Supabase.Postgrest.Constants.NullPosition nullPosition) => Order(predicate, ordering, nullPosition);

    global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel> global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel>.Order(string foreignTable, string column, global::Supabase.Postgrest.Constants.Ordering ordering, global::Supabase.Postgrest.Constants.NullPosition nullPosition) => Order(foreignTable, column, ordering, nullPosition);

    global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel> global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel>.Range(int from) => Range(from);

    global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel> global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel>.Range(int from, int to) => Range(from, to);

    global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel> global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel>.Select(string columnQuery) => Select(columnQuery);

    global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel> global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel>.Select(Expression<Func<TModel, object[]>> predicate) => Select(predicate);

    global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel> global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel>.Set(Expression<Func<TModel, object>> keySelector, object? value) => Set(keySelector, value);

    global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel> global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel>.Set(Expression<Func<TModel, KeyValuePair<object, object?>>> keyValuePairExpression) => Set(keyValuePairExpression);

    global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel> global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel>.Where(Expression<Func<TModel, bool>> predicate) => Where(predicate);
}
