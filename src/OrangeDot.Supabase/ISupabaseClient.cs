using System.Threading.Tasks;
using OrangeDot.Supabase.Urls;

namespace OrangeDot.Supabase;

public interface ISupabaseClient
{
    string Url { get; }

    string AnonKey { get; }

    SupabaseUrls Urls { get; }

    Task Ready { get; }
}
