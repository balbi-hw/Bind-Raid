# 환경 구조물 이미지 레퍼런스

2026-10-31. 내장 이미지 생성 도구로 제작. Tripo 변환용 입력 레퍼런스이며 실제 3D 모델은 포함하지 않습니다.

## 구성과 권장 모델링 치수

| 파일 | 내용 | 권장 치수 (m) |
|---|---|---|
| floor.png | 반복 배치 바닥 슬래브 | 4 × 4 × 두께 0.25 |
| wall.png | 직선 반복 벽, 기둥 없는 평평한 양 끝 | 길이 4 × 높이 3 × 두께 0.6 |
| stairs.png | 직선 계단 | 폭 2 × 수평 길이 4 × 높이 2 |
| cover.png | 낮은 석재 엄폐물 | 폭 3 × 높이 1.2 × 깊이 0.8 |
| pillar_intact.png | 파괴 가능 기둥 원형 | 축 폭 1, 높이 3.5, 받침 폭 1.4 |
| pillar_destroyed.png | 기둥 파괴 후 잔해 | 원형 받침과 낮은 잔존부, 큰 파편 |
| barricade_intact.png | 파괴 가능 목재 바리케이드 원형 | 폭 3 × 높이 1.4 × 깊이 0.9 |
| barricade_destroyed.png | 바리케이드 파괴 후 잔해 | 부러진 기둥과 목재 파편 |

치수는 이미지에 보장되는 측정값이 아닌 Blender 정리 기준입니다. 벽 양 끝과 바닥 외곽을 정확한 격자에 맞추고 반복 배치 연결부를 확인하세요. 바닥의 무늬가 타일링 텍스처처럼 완전히 이어지는 것은 보장하지 않습니다.

Tripo에는 각 이미지를 하나씩 입력하세요. 파괴 전후 이미지는 형태 설계 참고이며 동일 토폴로지나 파편 대응을 보장하지 않습니다. Blender에서 원형 메시를 분할해 파편을 만들고 파괴 후 레퍼런스의 배치와 단면을 참고하면 원형과 파괴 상태의 대응을 유지할 수 있습니다. 고정 구조물은 리깅 없이 배치 가능하며, 파괴물은 별도 파편과 피벗을 정리한 후 필요한 애니메이션 방식을 적용하세요.

## 생성 프롬프트

공통: Single isolated game environment asset for image-to-3D reconstruction in Tripo. Stylized semi-realistic ancient raid arena, warm gray chunky dressed stone and restrained dark iron reinforcement, desaturated weathering, simple readable solid geometry. Orthographic three-quarter view slightly above, full uncropped object, neutral studio light, transparent background. No characters, scenery, text, collage or ground plane.

- floor: square modular flat stone arena floor slab, large fitted paving, straight matching square edges, no protruding border.
- wall: single straight reusable stone wall segment, flat top, identical flat vertical end faces, no end pillars, corners or attached ground, horizontal masonry courses to edges.
- stairs: straight stone staircase, eight broad equal steps, solid closed wedge underneath, no railing or platform.
- cover: sturdy low masonry cover barrier, broad flat top, chunky tapered base, iron corner bands.
- pillar_intact: square stone pillar with square base and cap, four large courses, two iron bands, small cracks but fully standing.
- pillar_destroyed: matching design of square stone pillar after collapse, base with jagged low stump, four large shaft and cap fragments nearby, bent iron bands, pale broken stone interiors, no dust or floating debris.
- barricade_intact: three thick horizontal dark brown timber beams secured to two vertical posts with iron brackets and triangular feet, no spikes.
- barricade_destroyed: matching design after destruction, broken post stumps and feet, snapped beams collapsed into six large wood fragments with bent brackets, no dust or floating debris.
