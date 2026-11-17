# GitHub 저장소 제출 순서

1. [GitHub 새 저장소 만들기](https://github.com/new)에서 개인 저장소를 만듭니다.
   - 예시 이름: `firefly-nocturne`
   - 교수님이 URL로 열어볼 수 있게 **Public**으로 설정합니다.
   - **Add a README file**, **Add .gitignore**, **Choose a license**는 선택하지 않고 빈 저장소로 만듭니다. 프로젝트에 README와 Unity용 .gitignore가 이미 들어 있습니다.
2. PowerShell을 열고 다음 명령을 실행합니다. `내아이디`와 저장소 이름을 본인 것으로 바꿉니다.

```powershell
cd C:\Untiy\GameEngineWork
git init
git add Assets Packages ProjectSettings README.md GIT_SUBMISSION.md .gitignore .gitattributes
git status --short
```

3. 목록에 `Assets`, `Packages`, `ProjectSettings`, 문서와 Git 설정만 있는지 확인합니다. `Library`, `Logs`, `UserSettings`, 출력·임시 파일은 없어야 합니다. 이어서 실행합니다.

```powershell
git commit -m "Create Firefly Nocturne fireworks scene"
git branch -M main
git remote add origin https://github.com/내아이디/firefly-nocturne.git
git push -u origin main
```

4. 로그인 창이 나오면 본인 GitHub 계정으로 로그인합니다. Git 커밋 사용자 정보가 없다는 오류가 나오면 아래 두 줄에 본인 정보를 넣은 뒤 `git commit`부터 다시 실행합니다.

```powershell
git config user.name "본인이름"
git config user.email "본인 GitHub 이메일 또는 GitHub 비공개 커밋 이메일"
```

5. 브라우저에서 저장소를 열어 다음을 확인합니다.
   - README가 보임.
   - `Assets/Scenes/GameScene.unity`가 있음.
   - `Assets/FireflyFestival/Audio/An Old Legend - TTRM.mp3`와 `.meta`가 있음.
   - `Packages/manifest.json`, `Packages/packages-lock.json`, `ProjectSettings/ProjectVersion.txt`가 있음.
   - `Library`는 올라가지 않음.
6. 과제 제출란에 아래 형식으로 입력합니다. **저장소 URL**을 제출하면 되며 Unity 프로젝트 폴더 경로나 개별 파일 링크를 제출하지 않습니다.

```text
저장소: https://github.com/내아이디/firefly-nocturne
작품명: Firefly Nocturne · 반딧불의 야상곡
Unity: 6000.3.23f1
실행: Assets/Scenes/GameScene.unity → Play
연출: 노랑·연두 중심의 반딧불 불꽃놀이. 음악 전곡에 맞춘 도입, 전개, 절정, 피날레와 잔광 마무리.
BGM: TTRM - 오랜 전설 이야기 (완성)
```

첨부된 과제 안내의 마감은 **2026년 11월 20일 금요일 오후 11:59**입니다.

Git 저장소 생성·커밋·원격 연결·푸시는 사용자가 이 순서대로 진행합니다. 씬 제작 작업에서는 아직 Git 저장소를 만들거나 GitHub로 업로드하지 않았습니다.

명령 순서 참고: [GitHub 공식 문서 — 로컬 코드 GitHub에 추가하기](https://docs.github.com/en/migrations/importing-source-code/using-the-command-line-to-import-source-code/adding-locally-hosted-code-to-github?platform=windows).
