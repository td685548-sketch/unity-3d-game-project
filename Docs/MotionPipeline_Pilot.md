# Thử pipeline Rokoko → Blender → Unity

## Kết quả hiện tại

Đã nhập FBX người dùng xuất từ Rokoko Studio, retarget lên đúng rig Idle của nhân vật game, bake hai clip và quay so sánh trong Unity. Bản thử đã được chuyển tiếp thành clip Humanoid và gán vào Animator gameplay theo yêu cầu người dùng.

## Nguồn

- Bản FBX gốc được giữ tại ArtSource/MotionPipeline/Rokoko_Combined_Raw.fbx.
- File xuất có một take 10 giây (301 frame ở 30 fps), không phải hai FBX. Frame đầu là T-pose.
- Niệm quyết: frame 31–211, 6 giây. Ra tay/phóng kiếm: frame 211–301, 3 giây. Đây là cách tách thử theo động tác quan sát, chưa đồng bộ với thời điểm sinh kiếm trong gameplay.
- Tác vụ Rokoko Vision 2,7 giây trước đó vẫn được ghi trong pilot.json để lưu lịch sử. Chưa xác minh FBX 10 giây này đến từ tác vụ đó, không coi hai nguồn là cùng một take.

## Xử lý trong Blender

Target là rig từ NhanVat@Idle.fbx (25 xương). Chuyển thân trên theo hướng xương và hiệu chỉnh bone roll, giữ chân/hips theo tư thế Idle. Làm mượt quaternion 5 frame với trọng số đối xứng, liên tục dấu quaternion, chuyển vào/ra 0,4 giây. Không có xương ngón tay trên target nên chưa thể sao chép thủ quyết bằng từng ngón.

Bản chỉnh được: ArtSource/Blender/NiemQuyet_Retarget_Pilot.blend.
Script chạy lại trên scene này: ArtSource/MotionPipeline/retarget_pilot.py.
FBX dùng FBX_SCALE_ALL để tránh root scale 100 trong Unity.

## Asset Unity

Trong Assets/_Game/Models/Characters/NhanVat/MotionPilot/:

- NiemQuyet_Rokoko_Pilot.fbx và PhongKiem_Rokoko_Pilot.fbx: FBX đã bake, import Generic.
- NiemQuyet_GameRig_Pilot.anim và PhongKiem_GameRig_Pilot.anim: 250 đường cong/clip, bỏ cấp PilotTarget để trùng đường dẫn xương của model game. Đây là clip transform trực tiếp, không phải clip Humanoid muscle.

Hai clip Generic chỉ là dữ liệu trung gian. RokokoGameplayBuilder.Bake dùng HumanPoseHandler và Avatar NhanVat@Idle để chuyển pose thành 95 muscle curves + RootT/RootQ (102 curves/clip), dùng ClampedAuto cho muscle curves để giữ vận tốc nội suy mượt giữa các frame. Clip sản xuất là NiemQuyet_Rokoko.anim và PhongKiem_Rokoko.anim, humanMotion=True.

RokokoGameplayBuilder.Apply gán clip niệm quyết vào trạng thái NiemQuyet (tốc độ 1,5, khoảng 4 giây); gán clip xuất kiếm vào TayGiu/TayThu. AvatarMask thân trên không tác động root và chân. PlayerAnimator phát phần đưa tay trong 0,36 giây rồi giữ khung 1,066667 giây của clip xuất kiếm. ShootPoseGate chỉ cho kiếm phóng khi phần đưa tay kết thúc. Đang giữ tay thì phát tiếp theo không phải chờ thêm.

Sau R: hết 0,35 giây chờ, tay phát phần thu với tốc độ 1,4 rồi hòa trộn về locomotion trong 0,2 giây. Cú giật tay phụ giảm từ 8 xuống 2 độ, thời gian 0,16 giây. Bị đánh/chết/quyền cước vẫn có quyền ngắt lớp thân trên.

Menu Phàm Kiếm Lục > Animation > Build direct rig pilot clips tạo lại .anim và giữ GUID. Menu Quay so sánh Rokoko Pilot quay 216 frame trong scene tạm, sau đó đóng scene và trả về SanTap.

## Kiểm tra và video

Đã sample hai .anim trên rig Idle trong Unity ở 30 fps: mỗi clip có 250 đường cong, độ trượt hai bàn chân đo được 0 m. Clip niệm quyết 6 s, ra tay 3 s.

Video: Assets/Screenshots/motion_rokoko_pilot_compare.mp4 (9 giây, 24 fps). Bên trái là animation cũ, bên phải bản retarget. Clip cũ được đổi tốc độ theo thời lượng bản thử và căn chân xuống mặt đất chỉ để trình bày so sánh. Model dùng vật liệu preview; video không đánh giá VFX/gameplay.

Scene SanTap được lưu tham chiếu clip và thông số PlayerAnimator mới; Animator controller đã đổi ba motion, không đổi lớp locomotion/nhảy/đánh cận chiến. Cache và frame quay nằm trong Library/MotionPilot, không đưa vào Git.

Video gameplay sau tích hợp: Assets/Screenshots/rokoko_animation_gameplay.mp4 (11 giây), quay trực tiếp trong SanTap Play Mode. Menu Animation > Quay và kiểm tra animation gameplay thực hiện 13 kiểm tra: niệm quyết, chờ gate, phóng sau gate, phát tiếp tức thời, giữ tay, đúng hold frame, root không dịch chuyển, R, thu tay và trở về locomotion. Báo cáo: Library/MotionPilot/GameplayValidation.txt.

Sau tích hợp, 24/24 kiểm tra đấu phi kiếm đều qua; báo cáo Library/MotionPilot/SwordRegression.txt.

## Hướng nhìn

Khi bake Humanoid, RokokoGameplayBuilder chỉnh ánh nhìn về phía trước và ngẩng nhẹ 3 độ, chia độ nâng cho cổ (55%) và đầu (45%). Giữ hướng quay ngang/nghiêng của động tác gốc, hòa trộn hiệu chỉnh trong 0,4 giây ở hai đầu clip. Hiệu chỉnh nằm trong hai clip animation, không thêm phép xoay đầu toàn cục vào nhân vật.
