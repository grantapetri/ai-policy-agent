import os
import numpy as np
from dotenv import load_dotenv
from openai import OpenAI

# config

load_dotenv()

POLICY_FILE     = "policy.md"

EMBEDDING_MODEL = "text-embedding-3-small"
GPT_MODEL       = "gpt-5"

SIMILARITY_THRESHOLD = 0.40

client = OpenAI(api_key=os.getenv("OPEN_API_KEY"))

# load policy

def load_policy():
    with open(POLICY_FILE, "r", encoding="utf-8") as file:
        return file.read()

# split policy into chunks

def chunk_policy(policy):
    sections = policy.split("### ")

    chunks = []

    for section in sections:
        section = section.strip()

        if section:
            chunks.append(section)

    return chunks

# create embedding

def create_embedding(text):
    response = client.embeddings.create(
        model = EMBEDDING_MODEL,
        input = text
    )

    return response.data[0].embedding

# create policy vector store

def create_policy_index(chunks):
    index = []

    for chunk in chunks:
        print("Embedding policy chunk...")

        vector = create_embedding(chunk)

        index.append({
            "text": chunk,
            "vector": vector,
        })

    return index

# calculate similarity

def cosine_similarity(vector_a, vector_b):
    a = np.array(vector_a)
    b = np.array(vector_b)

    return np.dot(a, b) / (
        np.linalg.norm(a) * np.linalg.norm(b)
    )

# find relavent policy

def find_matching_policy(user_inp, policy_idx):
    input_vec = create_embedding(user_inp)

    matches = []

    for item in policy_idx:
        similarity = cosine_similarity(
            input_vec,
            item["vector"]
        )

        matches.append({
            "text": item["text"],
            "similarity": similarity,
        })

    matches.sort(
        key=lambda x: x["similarity"],
        reverse=True
    )

    return matches

# ask GPT to make decision

def make_policy_decision(user_inp, matches):

    best_match = matches[0]

    if best_match["similarity"] < SIMILARITY_THRESHOLD:
        return {
            "decision": "NO MATCH",
            "reason"  : "No sufficiently relavent policy was found"
        }

    policy_context = best_match["text"]

    prompt = f"""
    You are a policy decision engine.
    You MUST base your decision on the policy provided below.
    Do NOT invent policy rules.

    POLICY:
    {policy_context}

    INPUT:
    {user_inp}

    Determine the decision the policy requires

    Return your answer in this format:

    Decision: <decision>
    Reason  : <short explanation>
    """

    response = client.responses.create(
        model=GPT_MODEL,
        input=prompt
    )

    return {
        "decision"      : response.output_text,
        "similarity"    : best_match["similarity"],
        "matched_policy": policy_context,
    }

# main program

def main():

    print("Loading policy...")

    policy = load_policy()

    print("Splitting policy into chunks...")

    chunks = chunk_policy(policy)

    print(f"Created {len(chunks)} policy chunks.")

    print("Creating policy embeddings...")

    policy_index = create_policy_index(chunks)

    print("Policy engine ready.\n")

    while True:

        user_inp = input("Enter a policy question (or 'quit'): ")

        if user_inp.lower() == "quit":
            break

        matches = find_matching_policy(
            user_inp,
            policy_index
        )

        print(
            f"\nBest similarity: "
            f"{matches[0]['similarity']:.3f}"
        )

        result = make_policy_decision(
            user_inp,
            matches
        )

        print("\n--- RESULT ---")

        print(result["decision"])

        if "matched_policy" in result:
            print("\n--- MATCHED POLICY ---")
            print(result["matched_policy"])