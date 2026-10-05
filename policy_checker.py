import subprocess

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


def main():

    print("Getting recent code changes...\n")

    diff = get_git_diff()

    if not diff:
        print("No code changes found.")
        return

    print("----- CODE CHANGES -----")
    print(diff)
    print("------------------------")


if __name__ == "__main__":
    main()