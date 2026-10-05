import os
import re
import subprocess
import numpy as np

from dotenv import load_dotenv
from openai import OpenAI


load_dotenv()

POLICY_FILE = "policy.md"

EMBEDDING_MODEL = "text-embedding-3-small"

client = OpenAI()

def get_git_diff():

    result = subprocess.run(
        ["git", "diff", "HEAD~1", "HEAD"],
        capture_output=True,
        text=True
    )

    if result.returncode != 0:

        print("Could not get git diff.")
        print(result.stderr)

        return None

    return result.stdout

def load_policy():
    with open(POLICY_FILE, "r", encoding="utf-8") as file:
        return file.read()


def parse_policy(policy):
    pattern = r"###\s+(SEC-\d+)\s*-\s*(.+?)(?=###\s+SEC-\d+|$)"

    matches = re.findall(
        pattern,
        policy,
        re.DOTALL
    )

    rules = []

    for policy_id, text in matches:

        lines = text.strip().splitlines()

        title = lines[0].strip()

        rules.append({
            "id": policy_id,
            "title": title,
            "text": text.strip()
        })

    return rules

def create_embedding(text):

    response = client.embeddings.create(
        model=EMBEDDING_MODEL,
        input=text
    )

    return response.data[0].embedding

def cosine_similarity(vector_a, vector_b):

    a = np.array(vector_a)
    b = np.array(vector_b)

    return np.dot(a, b) / (
        np.linalg.norm(a) * np.linalg.norm(b)
    )

def create_policy_index(rules):

    policy_index = []

    for rule in rules:

        print(f"Embedding {rule['id']}...")

        vector = create_embedding(rule["text"])

        policy_index.append({
            "id": rule["id"],
            "title": rule["title"],
            "text": rule["text"],
            "vector": vector
        })

    return policy_index

def find_relevant_policies(diff, policy_index):

    print("\nEmbedding code changes...")

    diff_vector = create_embedding(diff)

    matches = []

    for rule in policy_index:

        similarity = cosine_similarity(
            diff_vector,
            rule["vector"]
        )

        matches.append({
            "id": rule["id"],
            "title": rule["title"],
            "text": rule["text"],
            "similarity": similarity
        })

    matches.sort(
        key=lambda x: x["similarity"],
        reverse=True
    )

    return matches

def make_policy_decision(diff, matches):

    best_match = matches[0]

    prompt = f"""
You are a software policy enforcement engine.

Your job is to determine whether the following code change
complies with the provided security policy.

You MUST base your decision on the policy.
Do not invent additional rules.

POLICY
------
Policy ID: {best_match["id"]}
Policy Title: {best_match["title"]}

{best_match["text"]}

CODE CHANGE
-----------
{diff}

Return your answer in exactly this format:

Decision: PASS or FAIL
Policy: <policy ID>
Reason: <short explanation>
"""

    response = client.responses.create(
        model="gpt-5",
        input=prompt
    )

    return response.output_text


def main():

    print("Loading policy...\n")

    policy = load_policy()

    rules = parse_policy(policy)

    print(f"Found {len(rules)} policy rules.\n")

    print("Creating policy embeddings...\n")

    policy_index = create_policy_index(rules)

    print("\nGetting Git changes...\n")

    diff = get_git_diff()

    if not diff:
        print("No changes found.")
        return

    matches = find_relevant_policies(
        diff,
        policy_index
    )

    print("\n===== POLICY MATCHES =====\n")

    for match in matches:

        print(
            f"{match['id']} - "
            f"{match['title']}: "
            f"{match['similarity']:.3f}"
        )

    print("\n===== GPT POLICY DECISION =====\n")

    decision = make_policy_decision(
        diff,
        matches
    )

    print(decision)

    # Determine whether the policy check passed
    if "DECISION: FAIL" in decision.upper():

        print("\n❌ POLICY CHECK FAILED")
        exit(1)

    elif "DECISION: PASS" in decision.upper():

        print("\n✅ POLICY CHECK PASSED")
        exit(0)

    else:

        print("\n!! Could not determine policy decision.")
        exit(1)

if __name__ == "__main__":
    main()