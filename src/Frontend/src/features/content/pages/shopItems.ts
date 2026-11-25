import { SaveShopItemRequest, ShopItem, ShopItemState, ShopSkin } from '../../../types/api';

export const MAX_PRICE = 1_000_000;
export const MAX_TITLE = 40;
export const MAX_DESCRIPTION = 254;
export const MAX_COLLECTION = 60;

export const stateNames: Record<ShopItemState, string> = {
  Default: 'Mặc định', OnSale: 'Đang bán', Stopped: 'Ngừng bán', Incomplete: 'Chưa bán được',
};

const money = (value: number) => value.toLocaleString('vi-VN');

/** Giá người chơi nhìn thấy; 0 nghĩa là không bán bằng loại tiền đó. */
export const priceLabel = (item: { tokenpadPrice: number; elitePrice: number; state?: ShopItemState }): string => {
  if (item.state === 'Default') return 'Miễn phí';
  const parts: string[] = [];
  if (item.tokenpadPrice > 0) parts.push(`${money(item.tokenpadPrice)} TOKENPAD`);
  if (item.elitePrice > 0) parts.push(`${money(item.elitePrice)} ELITE TOKEN`);
  return parts.length ? parts.join(' / ') : 'Không bán';
};

/** Form điền sẵn từ vợt có model trong game. */
export const draftOf = (skin?: ShopSkin): SaveShopItemRequest => ({
  code: skin?.code ?? '', title: skin?.title ?? '', description: skin?.description ?? '', collection: skin?.collection ?? '',
  tokenpadPrice: 0, elitePrice: 0, premium: skin?.premium ?? false,
});

export const requestOf = (item: ShopItem): SaveShopItemRequest => ({
  code: item.code, title: item.title, description: item.description, collection: item.collection,
  tokenpadPrice: item.tokenpadPrice, elitePrice: item.elitePrice, premium: item.premium,
});

/** Lỗi đầu tiên của form, hoặc null. Máy chủ kiểm tra lại toàn bộ. */
export const problemOf = (form: SaveShopItemRequest, creating: boolean): string | null => {
  const within = (text: string, min: number, max: number) => text.trim().length >= min && text.trim().length <= max;
  const price = (value: number) => Number.isInteger(value) && value >= 0 && value <= MAX_PRICE;
  if (!form.code) return 'Chọn vợt cần đăng bán.';
  if (!within(form.title, 2, MAX_TITLE)) return `Tên vật phẩm phải có từ 2 đến ${MAX_TITLE} ký tự.`;
  if (!within(form.collection, 1, MAX_COLLECTION)) return `Bộ sưu tập phải có từ 1 đến ${MAX_COLLECTION} ký tự.`;
  if (!within(form.description, 1, MAX_DESCRIPTION)) return `Mô tả phải có từ 1 đến ${MAX_DESCRIPTION} ký tự.`;
  if (!price(form.tokenpadPrice) || !price(form.elitePrice)) return `Giá phải là số nguyên từ 0 đến ${money(MAX_PRICE)}.`;
  if (creating && form.tokenpadPrice === 0 && form.elitePrice === 0) return 'Nhập ít nhất một giá lớn hơn 0.';
  return null;
};
