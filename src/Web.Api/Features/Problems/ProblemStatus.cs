namespace Web.Api.Features.Problems;

/// <summary>Lifecycle: <c>Draft → Published → Archived</c> (DM-02). Only Published problems are visible to learners.</summary>
public enum ProblemStatus
{
    Draft,
    Published,
    Archived
}
