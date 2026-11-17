HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Phase 2 (docs/guide/03-worker-and-sandbox.md):
//   - NatsClient singleton + INatsJSContext (ConnectionStrings:Nats)
//   - DockerClient pointed at DOCKER_HOST (the sandbox daemon, never the host socket)
//   - SandboxOptions (Sandbox:Images, Sandbox:Runtime, Sandbox:MaxParallel)
//   - SubmissionConsumer, SandboxRunner, SandboxJanitor, problem-bundle cache

IHost host = builder.Build();
await host.RunAsync();
