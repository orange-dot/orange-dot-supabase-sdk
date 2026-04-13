const sessionStorageKey = "research-workspace-session";

const state = {
  bootstrap: null,
  session: loadStoredSession(),
  me: null,
  organizations: [],
  memberships: [],
  projects: [],
  experiments: [],
  runs: [],
  metrics: [],
  artifacts: [],
  decisions: [],
  watch: null,
  selected: {
    organizationId: null,
    projectId: null,
    experimentId: null,
    runId: null
  }
};

const elements = {
  sampleName: document.getElementById("sample-name"),
  supabaseUrl: document.getElementById("supabase-url"),
  swaggerLink: document.getElementById("swagger-link"),
  openApiLink: document.getElementById("openapi-link"),
  capabilityChips: document.getElementById("capability-chips"),
  bucketName: document.getElementById("bucket-name"),
  functionName: document.getElementById("function-name"),
  sessionSummary: document.getElementById("session-summary"),
  sessionPill: document.getElementById("session-pill"),
  sessionEmail: document.getElementById("session-email"),
  sessionUserId: document.getElementById("session-user-id"),
  sessionExpiry: document.getElementById("session-expiry"),
  flash: document.getElementById("flash"),
  authForm: document.getElementById("auth-form"),
  authEmail: document.getElementById("auth-email"),
  authPassword: document.getElementById("auth-password"),
  signUpButton: document.getElementById("sign-up-button"),
  signOutButton: document.getElementById("sign-out-button"),
  refreshButton: document.getElementById("refresh-button"),
  organizationSelect: document.getElementById("organization-select"),
  projectSelect: document.getElementById("project-select"),
  experimentSelect: document.getElementById("experiment-select"),
  runSelect: document.getElementById("run-select"),
  organizationsCount: document.getElementById("organizations-count"),
  projectsCount: document.getElementById("projects-count"),
  experimentsCount: document.getElementById("experiments-count"),
  runsCount: document.getElementById("runs-count"),
  workspaceHint: document.getElementById("workspace-hint"),
  organizationForm: document.getElementById("organization-form"),
  organizationName: document.getElementById("organization-name"),
  organizationsList: document.getElementById("organizations-list"),
  membershipForm: document.getElementById("membership-form"),
  membershipUserId: document.getElementById("membership-user-id"),
  membershipRole: document.getElementById("membership-role"),
  membershipsList: document.getElementById("memberships-list"),
  projectForm: document.getElementById("project-form"),
  projectName: document.getElementById("project-name"),
  projectsList: document.getElementById("projects-list"),
  experimentForm: document.getElementById("experiment-form"),
  experimentName: document.getElementById("experiment-name"),
  experimentSummary: document.getElementById("experiment-summary"),
  experimentStatus: document.getElementById("experiment-status"),
  experimentsList: document.getElementById("experiments-list"),
  baselineRun: document.getElementById("baseline-run"),
  runForm: document.getElementById("run-form"),
  runDisplayName: document.getElementById("run-display-name"),
  runNotes: document.getElementById("run-notes"),
  runStatus: document.getElementById("run-status"),
  runStatusForm: document.getElementById("run-status-form"),
  runStatusNext: document.getElementById("run-status-next"),
  runStatusNotes: document.getElementById("run-status-notes"),
  runsList: document.getElementById("runs-list"),
  metricForm: document.getElementById("metric-form"),
  metricName: document.getElementById("metric-name"),
  metricValue: document.getElementById("metric-value"),
  metricUnit: document.getElementById("metric-unit"),
  metricsList: document.getElementById("metrics-list"),
  artifactForm: document.getElementById("artifact-form"),
  artifactKind: document.getElementById("artifact-kind"),
  artifactFileName: document.getElementById("artifact-file-name"),
  artifactContentType: document.getElementById("artifact-content-type"),
  artifactContent: document.getElementById("artifact-content"),
  artifactsList: document.getElementById("artifacts-list"),
  baselineForm: document.getElementById("baseline-form"),
  baselineButton: document.getElementById("baseline-button"),
  decisionForm: document.getElementById("decision-form"),
  decisionTitle: document.getElementById("decision-title"),
  decisionSummary: document.getElementById("decision-summary"),
  decisionStatus: document.getElementById("decision-status"),
  decisionsList: document.getElementById("decisions-list"),
  watchStartButton: document.getElementById("watch-start-button"),
  watchRefreshButton: document.getElementById("watch-refresh-button"),
  watchId: document.getElementById("watch-id"),
  watchConnected: document.getElementById("watch-connected"),
  watchCount: document.getElementById("watch-count"),
  watchEvents: document.getElementById("watch-events")
};

let watchTimer = null;
let busyCounter = 0;

init().catch((error) => {
  console.error(error);
  setNotice("error", `Unable to initialize the cockpit. ${formatError(error)}`);
});

async function init() {
  bindEvents();
  await loadBootstrap();
  renderSession();
  renderWorkspace();

  if (state.session) {
    await refreshWorkspace();
  }
}

function bindEvents() {
  elements.authForm.addEventListener("submit", async (event) => {
    event.preventDefault();
    await runAction(() => authenticate("login"));
  });

  elements.signUpButton.addEventListener("click", async () => {
    await runAction(() => authenticate("signup"));
  });

  elements.signOutButton.addEventListener("click", () => {
    clearSession("Signed out. The browser token was removed.");
  });

  elements.refreshButton.addEventListener("click", async () => {
    await runAction(() => refreshWorkspace());
  });

  elements.organizationSelect.addEventListener("change", async () => {
    state.selected.organizationId = elements.organizationSelect.value || null;
    state.selected.projectId = null;
    state.selected.experimentId = null;
    state.selected.runId = null;
    clearWatch();
    await runAction(() => refreshWorkspace());
  });

  elements.projectSelect.addEventListener("change", async () => {
    state.selected.projectId = elements.projectSelect.value || null;
    state.selected.experimentId = null;
    state.selected.runId = null;
    clearWatch();
    await runAction(() => refreshWorkspace());
  });

  elements.experimentSelect.addEventListener("change", async () => {
    state.selected.experimentId = elements.experimentSelect.value || null;
    state.selected.runId = null;
    clearWatch();
    await runAction(() => refreshWorkspace());
  });

  elements.runSelect.addEventListener("change", async () => {
    state.selected.runId = elements.runSelect.value || null;
    await runAction(() => refreshWorkspace());
  });

  elements.organizationForm.addEventListener("submit", async (event) => {
    event.preventDefault();
    if (!ensureAuthenticated() || !elements.organizationName.value.trim()) {
      return;
    }

    await runAction(async () => {
      const organization = await apiFetch("/organizations", {
        method: "POST",
        body: {
          name: elements.organizationName.value.trim()
        }
      });

      elements.organizationForm.reset();
      state.selected.organizationId = organization.id;
      state.selected.projectId = null;
      state.selected.experimentId = null;
      state.selected.runId = null;
      setNotice("success", `Organization "${organization.name}" is ready.`);
      await refreshWorkspace();
    });
  });

  elements.membershipForm.addEventListener("submit", async (event) => {
    event.preventDefault();
    if (!ensureSelection("organizationId", "Select an organization before adding memberships.")) {
      return;
    }

    await runAction(async () => {
      const membership = await apiFetch(`/organizations/${state.selected.organizationId}/memberships`, {
        method: "POST",
        body: {
          userId: elements.membershipUserId.value.trim(),
          role: elements.membershipRole.value
        }
      });

      elements.membershipForm.reset();
      elements.membershipRole.value = state.bootstrap.roles[0];
      setNotice("success", `Added membership for ${shortId(membership.userId)} as ${membership.role}.`);
      await refreshWorkspace();
    });
  });

  elements.projectForm.addEventListener("submit", async (event) => {
    event.preventDefault();
    if (!ensureSelection("organizationId", "Select an organization before creating a project.")) {
      return;
    }

    await runAction(async () => {
      const project = await apiFetch(`/organizations/${state.selected.organizationId}/projects`, {
        method: "POST",
        body: {
          name: elements.projectName.value.trim()
        }
      });

      elements.projectForm.reset();
      state.selected.projectId = project.id;
      state.selected.experimentId = null;
      state.selected.runId = null;
      setNotice("success", `Project "${project.name}" was created.`);
      await refreshWorkspace();
    });
  });

  elements.experimentForm.addEventListener("submit", async (event) => {
    event.preventDefault();
    if (!ensureSelection("projectId", "Select a project before creating an experiment.")) {
      return;
    }

    await runAction(async () => {
      const experiment = await apiFetch(`/projects/${state.selected.projectId}/experiments`, {
        method: "POST",
        body: {
          name: elements.experimentName.value.trim(),
          summary: elements.experimentSummary.value.trim() || null,
          status: elements.experimentStatus.value
        }
      });

      elements.experimentForm.reset();
      elements.experimentStatus.value = state.bootstrap.experimentStatuses[1] || state.bootstrap.experimentStatuses[0];
      state.selected.experimentId = experiment.id;
      state.selected.runId = null;
      clearWatch();
      setNotice("success", `Experiment "${experiment.name}" was added.`);
      await refreshWorkspace();
    });
  });

  elements.runForm.addEventListener("submit", async (event) => {
    event.preventDefault();
    if (!ensureSelection("experimentId", "Select an experiment before creating a run.")) {
      return;
    }

    await runAction(async () => {
      const run = await apiFetch(`/experiments/${state.selected.experimentId}/runs`, {
        method: "POST",
        body: {
          displayName: elements.runDisplayName.value.trim(),
          notes: elements.runNotes.value.trim() || null,
          status: elements.runStatus.value
        }
      });

      elements.runForm.reset();
      elements.runStatus.value = state.bootstrap.runStatuses[1] || state.bootstrap.runStatuses[0];
      state.selected.runId = run.id;
      setNotice("success", `Run "${run.displayName}" is on the board.`);
      await refreshWorkspace();
    });
  });

  elements.runStatusForm.addEventListener("submit", async (event) => {
    event.preventDefault();
    if (!ensureSelection("runId", "Select a run before updating its status.")) {
      return;
    }

    await runAction(async () => {
      const run = await apiFetch(`/runs/${state.selected.runId}/status`, {
        method: "POST",
        body: {
          status: elements.runStatusNext.value,
          notes: elements.runStatusNotes.value.trim() || null
        }
      });

      elements.runStatusNotes.value = "";
      setNotice("success", `Run "${run.displayName}" is now ${run.status}.`);
      await refreshWorkspace();
      if (state.watch?.watchId) {
        await refreshWatch(true);
      }
    });
  });

  elements.metricForm.addEventListener("submit", async (event) => {
    event.preventDefault();
    if (!ensureSelection("runId", "Select a run before appending metrics.")) {
      return;
    }

    await runAction(async () => {
      const metric = await apiFetch(`/runs/${state.selected.runId}/metrics`, {
        method: "POST",
        body: {
          metricName: elements.metricName.value.trim(),
          metricValue: Number(elements.metricValue.value),
          metricUnit: elements.metricUnit.value.trim() || null
        }
      });

      elements.metricForm.reset();
      setNotice("success", `Metric "${metric.metricName}" was appended.`);
      await refreshWorkspace();
    });
  });

  elements.artifactForm.addEventListener("submit", async (event) => {
    event.preventDefault();
    if (!ensureSelection("runId", "Select a run before uploading artifacts.")) {
      return;
    }

    await runAction(async () => {
      const artifact = await apiFetch(`/runs/${state.selected.runId}/artifacts/text`, {
        method: "POST",
        body: {
          kind: elements.artifactKind.value,
          fileName: elements.artifactFileName.value.trim(),
          contentType: elements.artifactContentType.value.trim() || "text/plain",
          content: elements.artifactContent.value
        }
      });

      elements.artifactForm.reset();
      elements.artifactKind.value = state.bootstrap.artifactKinds[0];
      elements.artifactContentType.value = "text/plain";
      setNotice("success", `Artifact "${artifact.fileName}" is uploaded.`);
      await refreshWorkspace();
    });
  });

  elements.baselineForm.addEventListener("submit", async (event) => {
    event.preventDefault();
    if (!ensureSelection("experimentId", "Select an experiment before promoting a baseline.")
      || !ensureSelection("runId", "Select a run before promoting a baseline.")) {
      return;
    }

    await runAction(async () => {
      const baseline = await apiFetch(`/experiments/${state.selected.experimentId}/baseline`, {
        method: "POST",
        body: {
          runId: state.selected.runId
        }
      });

      setNotice("success", `Run ${shortId(baseline.promotedRunId)} is now the experiment baseline.`);
      await refreshWorkspace();
    });
  });

  elements.decisionForm.addEventListener("submit", async (event) => {
    event.preventDefault();
    if (!ensureSelection("projectId", "Select a project before recording a decision.")) {
      return;
    }

    await runAction(async () => {
      const decision = await apiFetch(`/projects/${state.selected.projectId}/decisions`, {
        method: "POST",
        body: {
          title: elements.decisionTitle.value.trim(),
          summary: elements.decisionSummary.value.trim() || null,
          status: elements.decisionStatus.value,
          experimentId: state.selected.experimentId,
          baselineRunId: state.selected.runId
        }
      });

      elements.decisionForm.reset();
      elements.decisionStatus.value = state.bootstrap.decisionStatuses[0];
      setNotice("success", `Decision "${decision.title}" has been recorded.`);
      await refreshWorkspace();
    });
  });

  elements.watchStartButton.addEventListener("click", async () => {
    await runAction(async () => {
      if (!ensureSelection("experimentId", "Select an experiment before starting a watch.")) {
        return;
      }

      const watch = await apiFetch(`/experiments/${state.selected.experimentId}/watchers`, {
        method: "POST"
      });

      state.watch = watch;
      setNotice("success", `Watch ${shortId(watch.watchId)} is live for the selected experiment.`);
      startWatchPolling();
      await refreshWatch(true);
    });
  });

  elements.watchRefreshButton.addEventListener("click", async () => {
    await runAction(() => refreshWatch(false));
  });
}

async function loadBootstrap() {
  state.bootstrap = await publicFetch("/ui/bootstrap");

  elements.sampleName.textContent = state.bootstrap.sample;
  elements.supabaseUrl.textContent = state.bootstrap.supabaseUrl;
  elements.supabaseUrl.href = state.bootstrap.supabaseUrl;
  elements.swaggerLink.href = state.bootstrap.swaggerUrl;
  elements.openApiLink.href = state.bootstrap.openApiUrl;
  elements.bucketName.textContent = state.bootstrap.bucket;
  elements.functionName.textContent = state.bootstrap.function;
  elements.capabilityChips.innerHTML = state.bootstrap.capabilities
    .map((item) => `<span class="chip">${escapeHtml(item)}</span>`)
    .join("");

  populateSelect(elements.membershipRole, state.bootstrap.roles, state.bootstrap.roles[0], "Select role");
  populateSelect(elements.experimentStatus, state.bootstrap.experimentStatuses, state.bootstrap.experimentStatuses[1] || state.bootstrap.experimentStatuses[0], "Select status");
  populateSelect(elements.runStatus, state.bootstrap.runStatuses, state.bootstrap.runStatuses[1] || state.bootstrap.runStatuses[0], "Select status");
  populateSelect(elements.runStatusNext, state.bootstrap.runStatuses, state.bootstrap.runStatuses[1] || state.bootstrap.runStatuses[0], "Select status");
  populateSelect(elements.artifactKind, state.bootstrap.artifactKinds, state.bootstrap.artifactKinds[0], "Select kind");
  populateSelect(elements.decisionStatus, state.bootstrap.decisionStatuses, state.bootstrap.decisionStatuses[0], "Select status");
  elements.artifactContentType.value = "text/plain";
}

async function authenticate(mode) {
  const email = elements.authEmail.value.trim();
  const password = elements.authPassword.value.trim();

  if (!email || !password) {
    setNotice("error", "Email and password are required.");
    return;
  }

  const path = mode === "signup" ? "/ui/auth/signup" : "/ui/auth/login";
  const verb = mode === "signup" ? "created and signed in" : "signed in";

  const session = await publicFetch(path, {
    method: "POST",
    body: {
      email,
      password
    }
  });

  state.session = {
    ...session,
    createdAt: Date.now()
  };
  persistSession();
  renderSession();
  setNotice("success", `${session.email} is ${verb}.`);
  await refreshWorkspace();
}

async function refreshWorkspace() {
  if (!state.session?.accessToken) {
    resetWorkspaceCollections();
    renderWorkspace();
    return;
  }

  beginBusy();

  try {
    const me = await apiFetch("/me");
    const organizations = await apiFetch("/me/organizations");

    state.me = me;
    state.organizations = organizations;
    syncSelection("organizationId", state.organizations.map((item) => item.id));

    state.projects = await apiFetch("/projects");

    const visibleProjects = getVisibleProjects();
    syncSelection("projectId", visibleProjects.map((item) => item.id));

    const membershipsPromise = state.selected.organizationId
      ? apiFetch(`/organizations/${state.selected.organizationId}/memberships`)
      : Promise.resolve([]);
    const experimentsPromise = state.selected.projectId
      ? apiFetch(`/projects/${state.selected.projectId}/experiments`)
      : Promise.resolve([]);
    const decisionsPromise = state.selected.projectId
      ? apiFetch(`/projects/${state.selected.projectId}/decisions`)
      : Promise.resolve([]);

    const [memberships, experiments, decisions] = await Promise.all([
      membershipsPromise,
      experimentsPromise,
      decisionsPromise
    ]);

    state.memberships = memberships;
    state.experiments = experiments;
    state.decisions = decisions;
    syncSelection("experimentId", state.experiments.map((item) => item.id));

    state.runs = state.selected.experimentId
      ? await apiFetch(`/experiments/${state.selected.experimentId}/runs`)
      : [];
    syncSelection("runId", state.runs.map((item) => item.id));

    const [metrics, artifacts] = await Promise.all([
      state.selected.runId ? apiFetch(`/runs/${state.selected.runId}/metrics`) : Promise.resolve([]),
      state.selected.runId ? apiFetch(`/runs/${state.selected.runId}/artifacts`) : Promise.resolve([])
    ]);

    state.metrics = metrics;
    state.artifacts = artifacts;

    if (state.watch?.watchId) {
      await refreshWatch(true);
    }
  } catch (error) {
    if (error.status === 401) {
      clearSession("The access token expired or is no longer valid. Sign in again.");
      return;
    }

    throw error;
  } finally {
    endBusy();
    renderSession();
    renderWorkspace();
  }
}

async function refreshWatch(silent) {
  if (!state.watch?.watchId || !state.session?.accessToken) {
    renderWatch();
    return;
  }

  try {
    state.watch = await apiFetch(`/watchers/${state.watch.watchId}`);
    renderWatch();
  } catch (error) {
    if (error.status === 403 || error.status === 404) {
      stopWatchPolling();
    }

    if (!silent) {
      setNotice("error", formatError(error));
    }
  }
}

function renderSession() {
  if (!state.session) {
    elements.sessionPill.textContent = "Signed out";
    elements.sessionPill.className = "badge badge-idle";
    elements.sessionSummary.textContent = "Anonymous browser";
    elements.sessionEmail.textContent = "No active session";
    elements.sessionUserId.textContent = "-";
    elements.sessionExpiry.textContent = "Sign in to load workspace data";
    elements.signOutButton.disabled = true;
    return;
  }

  elements.sessionPill.textContent = "Access token loaded";
  elements.sessionPill.className = "badge";
  elements.sessionSummary.textContent = state.session.email;
  elements.sessionEmail.textContent = state.session.email;
  elements.sessionUserId.textContent = state.session.userId;
  elements.sessionExpiry.textContent = formatExpiry(state.session);
  elements.signOutButton.disabled = false;
}

function renderWorkspace() {
  const visibleProjects = getVisibleProjects();
  const selectedExperiment = state.experiments.find((item) => item.id === state.selected.experimentId) ?? null;

  populateSelect(elements.organizationSelect, state.organizations, state.selected.organizationId, "Select organization", (item) => `${item.name} (${item.role})`);
  populateSelect(elements.projectSelect, visibleProjects, state.selected.projectId, "Select project", (item) => item.name);
  populateSelect(elements.experimentSelect, state.experiments, state.selected.experimentId, "Select experiment", (item) => `${item.name} (${item.status})`);
  populateSelect(elements.runSelect, state.runs, state.selected.runId, "Select run", (item) => `${item.displayName} (${item.status})`);

  elements.organizationsCount.textContent = String(state.organizations.length);
  elements.projectsCount.textContent = String(visibleProjects.length);
  elements.experimentsCount.textContent = String(state.experiments.length);
  elements.runsCount.textContent = String(state.runs.length);
  elements.workspaceHint.textContent = buildWorkspaceHint();
  elements.baselineRun.textContent = selectedExperiment?.baselineRunId
    ? `Baseline ${shortId(selectedExperiment.baselineRunId)}`
    : "No baseline selected";

  renderLedger(
    elements.organizationsList,
    state.organizations,
    (organization) => `
      <article class="ledger-item ${organization.id === state.selected.organizationId ? "is-current" : ""}">
        <div class="ledger-main">
          <div>
            <div class="ledger-title">${escapeHtml(organization.name)}</div>
            <div class="ledger-subtitle">${escapeHtml(shortId(organization.id))}</div>
          </div>
          <span class="badge">${escapeHtml(organization.role)}</span>
        </div>
        <div class="ledger-meta">
          <span>Inserted ${escapeHtml(formatDate(organization.insertedAt))}</span>
        </div>
      </article>`,
    "No organizations are visible yet.");

  renderLedger(
    elements.membershipsList,
    state.memberships,
    (membership) => `
      <article class="ledger-item">
        <div class="ledger-main">
          <div>
            <div class="ledger-title">${escapeHtml(shortId(membership.userId))}</div>
            <div class="ledger-subtitle">${escapeHtml(membership.id)}</div>
          </div>
          <span class="badge">${escapeHtml(membership.role)}</span>
        </div>
        <div class="ledger-meta">
          <span>Added ${escapeHtml(formatDate(membership.insertedAt))}</span>
        </div>
      </article>`,
    "Select an organization to inspect memberships.");

  renderLedger(
    elements.projectsList,
    visibleProjects,
    (project) => `
      <article class="ledger-item ${project.id === state.selected.projectId ? "is-current" : ""}">
        <div class="ledger-main">
          <div>
            <div class="ledger-title">${escapeHtml(project.name)}</div>
            <div class="ledger-subtitle">${escapeHtml(shortId(project.id))}</div>
          </div>
          <span class="badge">${escapeHtml(project.visibility)}</span>
        </div>
        <div class="ledger-meta">
          <span>Org ${escapeHtml(shortId(project.organizationId))}</span>
          <span>${escapeHtml(formatDate(project.updatedAt))}</span>
        </div>
      </article>`,
    "No projects match the selected organization.");

  renderLedger(
    elements.experimentsList,
    state.experiments,
    (experiment) => `
      <article class="ledger-item ${experiment.id === state.selected.experimentId ? "is-current" : ""}">
        <div class="ledger-main">
          <div>
            <div class="ledger-title">${escapeHtml(experiment.name)}</div>
            <div class="ledger-subtitle">${escapeHtml(experiment.summary || "No summary yet")}</div>
          </div>
          <span class="badge">${escapeHtml(experiment.status)}</span>
        </div>
        <div class="ledger-meta">
          <span>${experiment.baselineRunId ? `Baseline ${escapeHtml(shortId(experiment.baselineRunId))}` : "No baseline"}</span>
          <span>${escapeHtml(formatDate(experiment.updatedAt))}</span>
        </div>
      </article>`,
    "Select a project to inspect experiments.");

  renderLedger(
    elements.runsList,
    state.runs,
    (run) => `
      <article class="ledger-item ${run.id === state.selected.runId ? "is-current" : ""}">
        <div class="ledger-main">
          <div>
            <div class="ledger-title">${escapeHtml(run.displayName)}</div>
            <div class="ledger-subtitle">${escapeHtml(run.notes || "No run notes")}</div>
          </div>
          <span class="badge">${escapeHtml(run.status)}</span>
        </div>
        <div class="ledger-meta">
          <span>By ${escapeHtml(shortId(run.createdBy))}</span>
          <span>${escapeHtml(formatDate(run.updatedAt))}</span>
        </div>
      </article>`,
    "Select an experiment to inspect runs.");

  renderLedger(
    elements.metricsList,
    state.metrics,
    (metric) => `
      <article class="ledger-item">
        <div class="ledger-main">
          <div>
            <div class="ledger-title">${escapeHtml(metric.metricName)}</div>
            <div class="ledger-subtitle">${escapeHtml(shortId(metric.id))}</div>
          </div>
          <span class="badge">${escapeHtml(formatMetric(metric.metricValue, metric.metricUnit))}</span>
        </div>
        <div class="ledger-meta">
          <span>${escapeHtml(formatDate(metric.insertedAt))}</span>
        </div>
      </article>`,
    "Select a run to inspect metrics.");

  renderLedger(
    elements.artifactsList,
    state.artifacts,
    (artifact) => `
      <article class="ledger-item">
        <div class="ledger-main">
          <div>
            <div class="ledger-title">${escapeHtml(artifact.fileName)}</div>
            <div class="ledger-subtitle">${escapeHtml(artifact.kind)} in ${escapeHtml(artifact.bucket)}</div>
          </div>
          <span class="badge">${escapeHtml(artifact.contentType || "unknown")}</span>
        </div>
        <div class="ledger-meta">
          <span>${artifact.downloadUrl ? `<a href="${escapeHtml(artifact.downloadUrl)}" target="_blank" rel="noreferrer">Signed download</a>` : "Pending object upload"}</span>
          <span>${escapeHtml(formatDate(artifact.insertedAt))}</span>
        </div>
      </article>`,
    "Select a run to inspect artifacts.");

  renderLedger(
    elements.decisionsList,
    state.decisions,
    (decision) => `
      <article class="ledger-item">
        <div class="ledger-main">
          <div>
            <div class="ledger-title">${escapeHtml(decision.title)}</div>
            <div class="ledger-subtitle">${escapeHtml(decision.summary || "No decision summary")}</div>
          </div>
          <span class="badge">${escapeHtml(decision.status)}</span>
        </div>
        <div class="ledger-meta">
          <span>${decision.baselineRunId ? `Baseline ${escapeHtml(shortId(decision.baselineRunId))}` : "No baseline reference"}</span>
          <span>${escapeHtml(formatDate(decision.updatedAt))}</span>
        </div>
      </article>`,
    "Select a project to inspect decisions.");

  renderWatch();
  updateControlStates();
}

function renderWatch() {
  const watch = state.watch;

  if (!watch?.watchId) {
    elements.watchId.textContent = "No active watch";
    elements.watchConnected.textContent = "Idle";
    elements.watchCount.textContent = "0";
    renderLedger(elements.watchEvents, [], () => "", "Start a watch on an experiment to stream run status snapshots.");
    return;
  }

  elements.watchId.textContent = watch.watchId;
  elements.watchConnected.textContent = watch.connected ? "Connected" : "Connecting";
  elements.watchCount.textContent = String((watch.events || []).length);

  renderLedger(
    elements.watchEvents,
    watch.events || [],
    (item) => `
      <article class="watch-event">
        <div class="watch-main">
          <div>
            <div class="ledger-title">${escapeHtml(item.eventType)}</div>
            <div class="ledger-subtitle">Run ${escapeHtml(shortId(item.runId))}</div>
          </div>
          <span class="badge">${escapeHtml(item.status)}</span>
        </div>
        <div class="ledger-meta">
          <span>${escapeHtml(formatDate(item.observedAt))}</span>
        </div>
      </article>`,
    "The watch is armed, but no run status events have been observed yet.");
}

function updateControlStates() {
  const isAuthenticated = Boolean(state.session?.accessToken);
  const hasOrganization = Boolean(state.selected.organizationId);
  const hasProject = Boolean(state.selected.projectId);
  const hasExperiment = Boolean(state.selected.experimentId);
  const hasRun = Boolean(state.selected.runId);

  [
    elements.organizationName,
    elements.membershipUserId,
    elements.membershipRole,
    elements.projectName,
    elements.experimentName,
    elements.experimentSummary,
    elements.experimentStatus,
    elements.runDisplayName,
    elements.runNotes,
    elements.runStatus,
    elements.runStatusNext,
    elements.runStatusNotes,
    elements.metricName,
    elements.metricValue,
    elements.metricUnit,
    elements.artifactKind,
    elements.artifactFileName,
    elements.artifactContentType,
    elements.artifactContent,
    elements.decisionTitle,
    elements.decisionSummary,
    elements.decisionStatus
  ].forEach((element) => {
    element.disabled = !isAuthenticated;
  });

  elements.organizationForm.querySelector("button").disabled = !isAuthenticated;
  elements.membershipForm.querySelector("button").disabled = !isAuthenticated || !hasOrganization;
  elements.projectForm.querySelector("button").disabled = !isAuthenticated || !hasOrganization;
  elements.experimentForm.querySelector("button").disabled = !isAuthenticated || !hasProject;
  elements.runForm.querySelector("button").disabled = !isAuthenticated || !hasExperiment;
  elements.runStatusForm.querySelector("button").disabled = !isAuthenticated || !hasRun;
  elements.metricForm.querySelector("button").disabled = !isAuthenticated || !hasRun;
  elements.artifactForm.querySelector("button").disabled = !isAuthenticated || !hasRun;
  elements.baselineButton.disabled = !isAuthenticated || !hasExperiment || !hasRun;
  elements.decisionForm.querySelector("button").disabled = !isAuthenticated || !hasProject;
  elements.watchStartButton.disabled = !isAuthenticated || !hasExperiment;
  elements.watchRefreshButton.disabled = !isAuthenticated || !state.watch?.watchId;
  elements.refreshButton.disabled = !isAuthenticated;
}

function populateSelect(select, items, selectedValue, placeholder, labelFactory) {
  const label = typeof labelFactory === "function"
    ? labelFactory
    : (item) => typeof item === "string" ? item : item.name ?? item.id;

  const options = [`<option value="">${escapeHtml(placeholder)}</option>`];
  for (const item of items) {
    const value = typeof item === "string" ? item : item.id;
    const selected = value === selectedValue ? " selected" : "";
    options.push(`<option value="${escapeHtml(value)}"${selected}>${escapeHtml(label(item))}</option>`);
  }

  select.innerHTML = options.join("");
  if (selectedValue && items.some((item) => (typeof item === "string" ? item : item.id) === selectedValue)) {
    select.value = selectedValue;
  }
}

function renderLedger(container, items, renderer, emptyText) {
  if (!items || items.length === 0) {
    container.innerHTML = `<div class="empty-state">${escapeHtml(emptyText)}</div>`;
    return;
  }

  container.innerHTML = items.map(renderer).join("");
}

function syncSelection(key, availableIds) {
  if (!availableIds.length) {
    state.selected[key] = null;
    return;
  }

  if (!availableIds.includes(state.selected[key])) {
    state.selected[key] = availableIds[0];
  }
}

function getVisibleProjects() {
  if (!state.selected.organizationId) {
    return state.projects;
  }

  return state.projects.filter((item) => item.organizationId === state.selected.organizationId);
}

function resetWorkspaceCollections() {
  state.me = null;
  state.organizations = [];
  state.memberships = [];
  state.projects = [];
  state.experiments = [];
  state.runs = [];
  state.metrics = [];
  state.artifacts = [];
  state.decisions = [];
  state.selected.organizationId = null;
  state.selected.projectId = null;
  state.selected.experimentId = null;
  state.selected.runId = null;
  clearWatch(false);
}

function clearSession(message) {
  state.session = null;
  resetWorkspaceCollections();
  persistSession();
  renderSession();
  renderWorkspace();
  if (message) {
    setNotice("success", message);
  }
}

function clearWatch(render = true) {
  stopWatchPolling();
  state.watch = null;
  if (render) {
    renderWatch();
  }
}

function startWatchPolling() {
  stopWatchPolling();
  watchTimer = window.setInterval(() => {
    refreshWatch(true).catch((error) => console.error(error));
  }, 2000);
}

function stopWatchPolling() {
  if (watchTimer) {
    window.clearInterval(watchTimer);
    watchTimer = null;
  }
}

function ensureAuthenticated() {
  if (!state.session?.accessToken) {
    setNotice("error", "Sign in before using the workspace controls.");
    return false;
  }

  return true;
}

function ensureSelection(key, message) {
  if (!ensureAuthenticated()) {
    return false;
  }

  if (!state.selected[key]) {
    setNotice("error", message);
    return false;
  }

  return true;
}

function beginBusy() {
  busyCounter += 1;
  document.body.style.cursor = "progress";
}

async function runAction(action) {
  try {
    await action();
  } catch (error) {
    console.error(error);
    setNotice("error", formatError(error));
  }
}

function endBusy() {
  busyCounter = Math.max(0, busyCounter - 1);
  if (busyCounter === 0) {
    document.body.style.cursor = "default";
  }
}

async function publicFetch(path, options = {}) {
  beginBusy();
  try {
    return await rawFetch(path, options);
  } finally {
    endBusy();
  }
}

async function apiFetch(path, options = {}) {
  if (!state.session?.accessToken) {
    throw new Error("A bearer token is required.");
  }

  beginBusy();
  try {
    return await rawFetch(path, {
      ...options,
      headers: {
        Authorization: `Bearer ${state.session.accessToken}`,
        ...(options.headers ?? {})
      }
    });
  } finally {
    endBusy();
  }
}

async function rawFetch(path, options) {
  const response = await fetch(path, {
    method: options.method ?? "GET",
    headers: {
      "Content-Type": "application/json",
      ...(options.headers ?? {})
    },
    body: options.body ? JSON.stringify(options.body) : undefined
  });

  const text = await response.text();
  const payload = text ? tryParseJson(text) : null;

  if (!response.ok) {
    const error = new Error(payload?.detail || response.statusText || "Request failed.");
    error.status = response.status;
    error.error = payload?.error;
    error.payload = payload;
    throw error;
  }

  return payload;
}

function persistSession() {
  if (!state.session) {
    window.localStorage.removeItem(sessionStorageKey);
    return;
  }

  window.localStorage.setItem(sessionStorageKey, JSON.stringify(state.session));
}

function loadStoredSession() {
  try {
    const raw = window.localStorage.getItem(sessionStorageKey);
    return raw ? JSON.parse(raw) : null;
  } catch {
    return null;
  }
}

function setNotice(kind, message) {
  elements.flash.className = `notice notice-${kind}`;
  elements.flash.textContent = message;
}

function buildWorkspaceHint() {
  if (!state.session) {
    return "Sign in to unlock organizations, projects, experiments, runs, artifacts, decisions, and watcher controls.";
  }

  if (!state.organizations.length) {
    return "Create your first organization, then assign members to test owner, editor, and viewer flows.";
  }

  if (!state.selected.projectId) {
    return "Select an organization and create a project to start structuring the research workspace.";
  }

  if (!state.selected.experimentId) {
    return "Create an experiment inside the selected project to activate runs, artifacts, and baselines.";
  }

  if (!state.selected.runId) {
    return "Create a run to append metrics, upload artifacts, start watchers, and promote a baseline.";
  }

  return `Focused on run ${shortId(state.selected.runId)} inside experiment ${shortId(state.selected.experimentId)}.`;
}

function formatExpiry(session) {
  if (!session.createdAt || !session.expiresIn) {
    return "Access token loaded";
  }

  const expiresAt = new Date(session.createdAt + (session.expiresIn * 1000));
  return `Expires around ${expiresAt.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}`;
}

function formatMetric(value, unit) {
  return unit ? `${value} ${unit}` : String(value);
}

function formatDate(value) {
  if (!value) {
    return "Unknown time";
  }

  const date = new Date(value);
  return date.toLocaleString([], {
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit"
  });
}

function shortId(value) {
  if (!value) {
    return "-";
  }

  return value.length > 12 ? `${value.slice(0, 8)}...${value.slice(-4)}` : value;
}

function formatError(error) {
  if (!error) {
    return "Unknown error.";
  }

  if (typeof error === "string") {
    return error;
  }

  if (error.payload?.detail) {
    return error.payload.detail;
  }

  return error.message || "Request failed.";
}

function tryParseJson(text) {
  try {
    return JSON.parse(text);
  } catch {
    return null;
  }
}

function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll("\"", "&quot;")
    .replaceAll("'", "&#39;");
}
