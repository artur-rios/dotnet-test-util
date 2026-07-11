// Several tests mutate the process-wide ASPNETCORE_ENVIRONMENT environment variable
// (the custom attributes and the web API tests). Disabling parallelization keeps those
// mutations from racing across test classes.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
