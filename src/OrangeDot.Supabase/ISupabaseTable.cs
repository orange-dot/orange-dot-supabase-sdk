using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace OrangeDot.Supabase;

public interface ISupabaseTable<TModel> : global::Supabase.Postgrest.Interfaces.IPostgrestTable<TModel>
    where TModel : global::Supabase.Postgrest.Models.BaseModel, new()
{
    new ISupabaseTable<TModel> And(List<global::Supabase.Postgrest.Interfaces.IPostgrestQueryFilter> filters);

    new ISupabaseTable<TModel> Columns(string[] columns);

    new ISupabaseTable<TModel> Columns(Expression<Func<TModel, object[]>> predicate);

    new ISupabaseTable<TModel> Filter<TCriterion>(
        string columnName,
        global::Supabase.Postgrest.Constants.Operator op,
        TCriterion criterion);

    new ISupabaseTable<TModel> Filter<TCriterion>(
        Expression<Func<TModel, object>> predicate,
        global::Supabase.Postgrest.Constants.Operator op,
        TCriterion criterion);

    new ISupabaseTable<TModel> Limit(int limit, string? foreignTableName = null);

    new ISupabaseTable<TModel> Match(Dictionary<string, string> query);

    new ISupabaseTable<TModel> Match(TModel model);

    new ISupabaseTable<TModel> Not(global::Supabase.Postgrest.Interfaces.IPostgrestQueryFilter filter);

    new ISupabaseTable<TModel> Not(
        string columnName,
        global::Supabase.Postgrest.Constants.Operator op,
        Dictionary<string, object> criteria);

    new ISupabaseTable<TModel> Not(
        Expression<Func<TModel, object>> predicate,
        global::Supabase.Postgrest.Constants.Operator op,
        Dictionary<string, object> criteria);

    new ISupabaseTable<TModel> Not<TCriterion>(
        string columnName,
        global::Supabase.Postgrest.Constants.Operator op,
        List<TCriterion> criteria);

    new ISupabaseTable<TModel> Not<TCriterion>(
        Expression<Func<TModel, object>> predicate,
        global::Supabase.Postgrest.Constants.Operator op,
        List<TCriterion> criteria);

    new ISupabaseTable<TModel> Not<TCriterion>(
        string columnName,
        global::Supabase.Postgrest.Constants.Operator op,
        TCriterion criterion);

    new ISupabaseTable<TModel> Not<TCriterion>(
        Expression<Func<TModel, object>> predicate,
        global::Supabase.Postgrest.Constants.Operator op,
        TCriterion criterion);

    new ISupabaseTable<TModel> Offset(int offset, string? foreignTableName = null);

    new ISupabaseTable<TModel> OnConflict(string columnName);

    new ISupabaseTable<TModel> OnConflict(Expression<Func<TModel, object>> predicate);

    new ISupabaseTable<TModel> Or(List<global::Supabase.Postgrest.Interfaces.IPostgrestQueryFilter> filters);

    new ISupabaseTable<TModel> Order(
        string column,
        global::Supabase.Postgrest.Constants.Ordering ordering,
        global::Supabase.Postgrest.Constants.NullPosition nullPosition = global::Supabase.Postgrest.Constants.NullPosition.First);

    new ISupabaseTable<TModel> Order(
        Expression<Func<TModel, object>> predicate,
        global::Supabase.Postgrest.Constants.Ordering ordering,
        global::Supabase.Postgrest.Constants.NullPosition nullPosition = global::Supabase.Postgrest.Constants.NullPosition.First);

    new ISupabaseTable<TModel> Order(
        string foreignTable,
        string column,
        global::Supabase.Postgrest.Constants.Ordering ordering,
        global::Supabase.Postgrest.Constants.NullPosition nullPosition = global::Supabase.Postgrest.Constants.NullPosition.First);

    new ISupabaseTable<TModel> Range(int from);

    new ISupabaseTable<TModel> Range(int from, int to);

    new ISupabaseTable<TModel> Select(string columnQuery);

    new ISupabaseTable<TModel> Select(Expression<Func<TModel, object[]>> predicate);

    new ISupabaseTable<TModel> Set(Expression<Func<TModel, object>> keySelector, object? value);

    new ISupabaseTable<TModel> Set(Expression<Func<TModel, KeyValuePair<object, object?>>> keyValuePairExpression);

    new ISupabaseTable<TModel> Where(Expression<Func<TModel, bool>> predicate);

    Task<global::Supabase.Realtime.Interfaces.IRealtimeChannel> On(
        global::Supabase.Realtime.PostgresChanges.PostgresChangesOptions.ListenType listenType,
        global::Supabase.Realtime.Interfaces.IRealtimeChannel.PostgresChangesHandler handler,
        string schema = "public",
        string? filter = null,
        Dictionary<string, string>? parameters = null,
        int timeoutMs = global::Supabase.Realtime.Constants.DefaultTimeout);
}
