const functionName = "orangedot-integration-smoke"

console.log(`${functionName} started`)

Deno.serve(async (req: Request) => {
  if (req.method !== "POST") {
    return new Response(
      JSON.stringify({
        ok: false,
        function: functionName,
        error: "method_not_allowed",
      }),
      {
        status: 405,
        headers: { "Content-Type": "application/json" },
      },
    )
  }

  const body = await req.json().catch(() => ({}))
  const source =
    typeof body?.source === "string" && body.source.length > 0
      ? body.source
      : "unknown"

  return new Response(
    JSON.stringify({
      ok: true,
      function: functionName,
      source,
    }),
    {
      headers: { "Content-Type": "application/json" },
    },
  )
})
