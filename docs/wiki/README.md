# Wiki source

These Markdown files are the GitHub Wiki content for [MetaQuestTrayTool](https://github.com/Eliminater74/MetaQuestTrayTool/wiki).

GitHub only creates `MetaQuestTrayTool.wiki.git` after the **first** page is saved in the website UI. After that, push from this folder:

```powershell
# from a clone of the wiki repo (master branch)
Copy-Item docs\wiki\*.md <wiki-clone>\
Copy-Item docs\wiki\_Sidebar.md, docs\wiki\_Footer.md <wiki-clone>\
```

Live wiki: https://github.com/Eliminater74/MetaQuestTrayTool/wiki
