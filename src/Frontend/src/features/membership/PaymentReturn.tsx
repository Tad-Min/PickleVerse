export function PaymentReturn() {
  return (
    <main className="grid min-h-screen place-items-center bg-background px-5 text-foreground">
      <section className="w-full max-w-md rounded-2xl border border-border bg-card px-7 py-8 shadow-lg">
        <p className="text-xs font-semibold tracking-widest text-muted-foreground">PICKLEVERSE</p>
        <h1 className="mt-3 text-2xl font-semibold">Quay lại game</h1>
        <p className="mt-4 leading-relaxed text-muted-foreground">
          Khi PayOS xác nhận thanh toán, gói thành viên và Elite Token sẽ được cập nhật trên tài khoản của bạn trong PickleVerse.
        </p>
        <p className="mt-4 text-sm text-muted-foreground">Bạn có thể đóng trang này.</p>
      </section>
    </main>
  );
}
