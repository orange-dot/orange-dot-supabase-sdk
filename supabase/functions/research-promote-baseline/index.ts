const functionName = "research-promote-baseline"

type BaselineRequest = {
  projectId?: string
  experimentId?: string
  runId?: string
}

function json(status: number, payload: Record<string, unknown>) {
  return new Response(JSON.stringify(payload), {
    status,
    headers: { "Content-Type": "application/json" },
  })
}

function tryDecodeJwtSubject(authHeader: string | null): string | null {
  if (!authHeader || !authHeader.startsWith("Bearer ")) {
    return null
  }

  const token = authHeader.slice("Bearer ".length).trim()
  const parts = token.split(".")

  if (parts.length !== 3) {
    return null
  }

  try {
    const normalized = parts[1].replace(/-/g, "+").replace(/_/g, "/")
    const padding = "=".repeat((4 - (normalized.length % 4)) % 4)
    const payload = JSON.parse(atob(normalized + padding))
    return typeof payload?.sub === "string" && payload.sub.length > 0
      ? payload.sub
      : null
  } catch {
    return null
  }
}

function isUuid(value: string | undefined): value is string {
  return typeof value === "string" &&
    /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value)
}

async function fetchJson(
  url: string,
  init: RequestInit,
): Promise<unknown> {
  const response = await fetch(url, init)
  const text = await response.text()

  if (!response.ok) {
    throw new Error(`request_failed:${response.status}:${text}`)
  }

  return text.length === 0 ? null : JSON.parse(text)
}

console.log(`${functionName} started`)

Deno.serve(async (req: Request) => {
  if (req.method !== "POST") {
    return json(405, {
      ok: false,
      function: functionName,
      error: "method_not_allowed",
    })
  }

  const supabaseUrl = Deno.env.get("SUPABASE_URL")
  const anonKey = Deno.env.get("SUPABASE_ANON_KEY")
  const serviceRoleKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")
  const authHeader = req.headers.get("Authorization")
  const userId = tryDecodeJwtSubject(authHeader)

  if (!supabaseUrl || !anonKey || !serviceRoleKey || !userId) {
    return json(401, {
      ok: false,
      function: functionName,
      error: "auth_invalid",
    })
  }

  const body = await req.json().catch(() => ({})) as BaselineRequest

  if (!isUuid(body.projectId) || !isUuid(body.experimentId) || !isUuid(body.runId)) {
    return json(400, {
      ok: false,
      function: functionName,
      error: "invalid_request",
    })
  }

  try {
    const projectResponse = await fetchJson(
      `${supabaseUrl}/rest/v1/research_projects?select=id,organization_id&id=eq.${body.projectId}&limit=1`,
      {
        headers: {
          apikey: serviceRoleKey,
          Authorization: `Bearer ${serviceRoleKey}`,
        },
      },
    ) as Array<{ id: string; organization_id: string }>

    const project = projectResponse[0]

    if (!project) {
      return json(404, {
        ok: false,
        function: functionName,
        error: "project_not_found",
      })
    }

    const membershipResponse = await fetchJson(
      `${supabaseUrl}/rest/v1/research_memberships?select=role&organization_id=eq.${project.organization_id}&user_id=eq.${userId}&limit=1`,
      {
        headers: {
          apikey: serviceRoleKey,
          Authorization: `Bearer ${serviceRoleKey}`,
        },
      },
    ) as Array<{ role: string }>

    const membership = membershipResponse[0]

    if (!membership || (membership.role !== "owner" && membership.role !== "editor")) {
      return json(403, {
        ok: false,
        function: functionName,
        error: "insufficient_role",
      })
    }

    const experimentResponse = await fetchJson(
      `${supabaseUrl}/rest/v1/research_experiments?select=id,project_id&id=eq.${body.experimentId}&project_id=eq.${body.projectId}&limit=1`,
      {
        headers: {
          apikey: serviceRoleKey,
          Authorization: `Bearer ${serviceRoleKey}`,
        },
      },
    ) as Array<{ id: string; project_id: string }>

    if (!experimentResponse[0]) {
      return json(404, {
        ok: false,
        function: functionName,
        error: "experiment_not_found",
      })
    }

    const runResponse = await fetchJson(
      `${supabaseUrl}/rest/v1/research_runs?select=id,experiment_id&id=eq.${body.runId}&experiment_id=eq.${body.experimentId}&limit=1`,
      {
        headers: {
          apikey: serviceRoleKey,
          Authorization: `Bearer ${serviceRoleKey}`,
        },
      },
    ) as Array<{ id: string; experiment_id: string }>

    if (!runResponse[0]) {
      return json(404, {
        ok: false,
        function: functionName,
        error: "run_not_found",
      })
    }

    const updateResponse = await fetch(
      `${supabaseUrl}/rest/v1/research_experiments?id=eq.${body.experimentId}`,
      {
        method: "PATCH",
        headers: {
          apikey: serviceRoleKey,
          Authorization: `Bearer ${serviceRoleKey}`,
          "Content-Type": "application/json",
          Prefer: "return=representation",
        },
        body: JSON.stringify({
          baseline_run_id: body.runId,
        }),
      },
    )

    const updateText = await updateResponse.text()

    if (!updateResponse.ok) {
      return json(500, {
        ok: false,
        function: functionName,
        error: "baseline_update_failed",
        detail: updateText,
      })
    }

    return json(200, {
      ok: true,
      function: functionName,
      projectId: body.projectId,
      experimentId: body.experimentId,
      promotedRunId: body.runId,
    })
  } catch (error) {
    return json(500, {
      ok: false,
      function: functionName,
      error: "unexpected_error",
      detail: error instanceof Error ? error.message : String(error),
    })
  }
})
