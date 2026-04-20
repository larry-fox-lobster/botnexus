import sys

with open('Q:/repos/botnexus/docs/planning/INDEX.md', 'r', encoding='utf-8') as f:
    content = f.read()

target = '| [feature-spec-driven-squad-automation]'
new_line = '| [feature-blazor-subagent-session-view](feature-blazor-subagent-session-view/design-spec.md) | \U0001f7e1 medium | draft | Jul \'26 | Read-only sub-agent session viewing in Blazor UI |\n'

content = content.replace(target, new_line + target)

with open('Q:/repos/botnexus/docs/planning/INDEX.md', 'w', encoding='utf-8') as f:
    f.write(content)

print('done')
