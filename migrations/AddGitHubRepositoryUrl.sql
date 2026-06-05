-- Migration: Add GitHubRepositoryUrl to Projects table
-- Date: 2026-06-02

ALTER TABLE "Projects" ADD COLUMN "GitHubRepositoryUrl" TEXT;
